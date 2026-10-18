import { marked } from 'marked';
import privacy from '../content/privacy.md';
import terms from '../content/terms-and-refunds.md';

function render(document: string): string {
  const tokens = marked.lexer(document);
  if (tokens[0]?.type === 'heading' && tokens[0].depth === 1) tokens.shift();
  // These documents are trusted repository content, never request input.
  return marked.parser(tokens);
}

export const policies = { privacy: render(privacy), terms: render(terms) };
