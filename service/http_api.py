"""Small WSGI activation endpoint; payment verification is intentionally separate."""

import json
import logging

from service.authorization import ActivationRejected


_MAX_REQUEST_BYTES = 4096


def _response(start_response, status, value, extra_headers=()):
    body = json.dumps(value, separators=(",", ":")).encode("utf-8")
    headers = [
        ("Content-Type", "application/json; charset=utf-8"),
        ("Content-Length", str(len(body))),
        ("Cache-Control", "no-store"),
        ("X-Content-Type-Options", "nosniff"),
    ]
    start_response(status, headers + list(extra_headers))
    return [body]


def create_activation_app(store):
    def application(environ, start_response):
        if environ.get("PATH_INFO") != "/activate":
            return _response(start_response, "404 Not Found", {"error": "Not found"})
        if environ.get("wsgi.url_scheme") != "https":
            return _response(start_response, "403 Forbidden", {"error": "HTTPS required"})
        if environ.get("REQUEST_METHOD") != "POST":
            return _response(start_response, "405 Method Not Allowed",
                             {"error": "Method not allowed"}, [("Allow", "POST")])
        content_type = environ.get("CONTENT_TYPE", "").split(";", 1)[0].strip().lower()
        if content_type != "application/json":
            return _response(start_response, "415 Unsupported Media Type",
                             {"error": "JSON required"})
        length_text = environ.get("CONTENT_LENGTH", "")
        if not length_text.isascii() or not length_text.isdigit():
            return _response(start_response, "411 Length Required",
                             {"error": "Content length required"})
        digits = length_text.lstrip("0") or "0"
        if len(digits) > 4 or int(digits) > _MAX_REQUEST_BYTES:
            return _response(start_response, "413 Content Too Large",
                             {"error": "Request too large"})
        length = int(digits)
        try:
            stream = environ["wsgi.input"]
            body = stream.read(length)
            if len(body) != length:
                raise ValueError("Incomplete request")
            request = json.loads(body.decode("utf-8"))
            if not isinstance(request, dict) or set(request) != {"token", "deviceId"}:
                raise ValueError("Invalid request")
        except (KeyError, AttributeError, UnicodeDecodeError, ValueError):
            return _response(start_response, "400 Bad Request",
                             {"error": "Invalid request"})
        try:
            envelope = store.redeem(request["token"], request["deviceId"])
        except ActivationRejected:
            return _response(start_response, "403 Forbidden",
                             {"error": "Activation rejected"})
        except Exception:
            logging.exception("Activation service failed")
            return _response(start_response, "500 Internal Server Error",
                             {"error": "Activation unavailable"})
        return _response(start_response, "200 OK", envelope)

    return application
