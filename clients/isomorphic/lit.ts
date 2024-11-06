/// <reference path="./lit.d.ts" />
export { css, LitElement, html } from 'npm:lit@3';
export { customElement, property } from 'npm:lit@3/decorators.js';
export { createRef, ref } from 'npm:lit-html@2/directives/ref.js';

export interface LitElementBuilder {
  (tagName: string, attrs: Record<string, unknown> | null, ...content: unknown[]): any
}
