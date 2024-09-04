/// <reference path="./lit.d.ts" />
export { LitElement, html } from 'npm:lit@3';
export { customElement, property } from 'npm:lit@3/decorators.js';

export interface LitElementBuilder {
  (tag: any, attrs: Record<string, any> | null, ...content: any[]): any
}

export const h: LitElementBuilder = function (tag, attrs, ...content) {
  // FIXME: Apply attributes and content to the new tag
  console.log('h', tag, attrs, content)
}
