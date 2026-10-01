"""Payment-provider-independent, single-use letsmall license issuance core."""

import base64
import hashlib
import json
import re
import secrets
import sqlite3
import time
from contextlib import contextmanager

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa


_TOKEN = re.compile(r"[A-Za-z0-9_-]{16,256}\Z")
_DEVICE = re.compile(r"[0-9a-f]{64}\Z")
_SIGNING_PREFIX = b"letsmall-license-v1\n"


class ActivationRejected(Exception):
    """The token cannot issue a license."""


class AuthorizationStore:
    def __init__(self, database_path, private_key_pem, clock=time.time):
        self._database_path = str(database_path)
        self._clock = clock
        self._key = serialization.load_pem_private_key(private_key_pem, password=None)
        if not isinstance(self._key, rsa.RSAPrivateKey) or not 2048 <= self._key.key_size <= 4096:
            raise ValueError("An RSA 2048-4096 private key is required")
        with self._transaction() as connection:
            connection.execute(
                "CREATE TABLE IF NOT EXISTS paid_orders ("
                "order_id TEXT PRIMARY KEY, token_digest TEXT NOT NULL UNIQUE, "
                "expires_at INTEGER NOT NULL, device_id TEXT)"
            )

    def _connect(self):
        return sqlite3.connect(self._database_path, timeout=5)

    @contextmanager
    def _transaction(self):
        connection = self._connect()
        try:
            connection.execute("BEGIN IMMEDIATE")
            yield connection
            connection.commit()
        except Exception:
            connection.rollback()
            raise
        finally:
            connection.close()

    def record_paid_order(self, order_id, ttl_seconds=600):
        """Called only by a future adapter after authenticating a paid callback."""
        if (not isinstance(order_id, str) or not order_id.strip() or
                len(order_id) > 128 or not 1 <= ttl_seconds <= 3600):
            raise ValueError("Invalid paid order")
        with self._transaction() as connection:
            if connection.execute(
                "SELECT 1 FROM paid_orders WHERE order_id = ?", (order_id,)
            ).fetchone():
                return None
            token = secrets.token_urlsafe(32)
            connection.execute(
                "INSERT INTO paid_orders VALUES (?, ?, ?, NULL)",
                (order_id, self._digest(token), int(self._clock()) + ttl_seconds),
            )
            return token

    def redeem(self, token, device_id):
        if (not isinstance(token, str) or not _TOKEN.fullmatch(token) or
                not isinstance(device_id, str) or not _DEVICE.fullmatch(device_id)):
            raise ActivationRejected("Activation rejected")
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT order_id, expires_at, device_id FROM paid_orders "
                "WHERE token_digest = ?", (self._digest(token),)
            ).fetchone()
            if not row or row[1] <= int(self._clock()) or row[2] is not None:
                raise ActivationRejected("Activation rejected")
            envelope = self._sign(row[0], device_id)
            connection.execute(
                "UPDATE paid_orders SET device_id = ? WHERE order_id = ?",
                (device_id, row[0]),
            )
            return envelope

    def public_key_xml(self):
        numbers = self._key.public_key().public_numbers()
        modulus = numbers.n.to_bytes(self._key.key_size // 8, "big")
        exponent = numbers.e.to_bytes((numbers.e.bit_length() + 7) // 8, "big")
        return ("<RSAKeyValue><Modulus>" + self._b64(modulus) +
                "</Modulus><Exponent>" + self._b64(exponent) +
                "</Exponent></RSAKeyValue>")

    def _sign(self, order_id, device_id):
        payload = json.dumps(
            {"v": 1, "product": "letsmall", "orderId": order_id,
             "deviceId": device_id}, separators=(",", ":"), ensure_ascii=True
        ).encode("utf-8")
        signature = self._key.sign(
            _SIGNING_PREFIX + payload, padding.PKCS1v15(), hashes.SHA256()
        )
        return {"payload": self._b64(payload), "signature": self._b64(signature)}

    @staticmethod
    def _digest(token):
        return hashlib.sha256(token.encode("ascii")).hexdigest()

    @staticmethod
    def _b64(data):
        return base64.b64encode(data).decode("ascii")
