/// <reference path="./lit.d.ts" />
export { css, LitElement, html } from 'npm:lit@3';
export { customElement, property } from 'npm:lit@3/decorators.js';

export interface LitElementBuilder {
  (tagName: string, attrs: Record<string, unknown> | null, ...content: unknown[]): any
}

export const h: LitElementBuilder = function (tagName, attrs, ...content) {
  const element = document.createElement(tagName);
  if (attrs) Object.keys(attrs).forEach(attr => {
    // FIXME: This likely doesn't fork for every attribute
    element[attr] = attrs[attr];
  });
  // FIXME: This likely isn't correct
  if (content.length) element.textContent = Array.from(content).map(x => x.toString()).join(" ");
  return element;
}
