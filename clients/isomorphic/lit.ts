export { LitElement, html } from 'npm:lit@3';
export { customElement, property } from 'npm:lit@3/decorators.js';

// See https://stackoverflow.com/a/78688588/1363247
export namespace h {
  export namespace JSX {
    export interface IntrinsicElements {
      // QUESTION: Make this more specific?
      [tag: string]: any
    }
  }
}

interface LitElementBuilder {
  (tag: any, attrs: Record<string, any> | null, ...content: any[]): any
}

const h: LitElementBuilder = function (tag, attrs, ...content) {
  console.log('h', tag, attrs, content)
}
