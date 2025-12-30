// deno-lint-ignore-file no-explicit-any
/// <reference path="./lit.d.ts" />
export { LitElement, css, html, type PropertyValues } from 'npm:lit@3';
export { customElement, property, state } from 'npm:lit@3/decorators.js';
export { createRef, ref, type Ref } from 'npm:lit@3/directives/ref.js';

export interface LitElementBuilder {
  (tagName: string, attrs: Record<string, unknown> | null, ...content: unknown[]): any
}

export const h: LitElementBuilder = function (tagName, attrs, ...content) {
  const element = document.createElement(tagName);
  if (attrs) Object.keys(attrs).forEach(attr => {
    // FIXME: This likely doesn't work for every attribute
    element.setAttribute(attr, (attrs[attr] as any).toString());
  });
  // FIXME: This likely isn't correct
  if (content.length) element.textContent = Array.from(content).map<string>(x => (x as any).toString()).join(" ");
  return element;
}
