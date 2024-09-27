import { css, customElement, LitElement, h, html } from './lit.ts';
// See https://usegpu.live/docs/reference-live-@use-gpu-live
// TODO: Contribute back these lit-html abstractions back to @use-gpu, i.e. a new `@use-gpu/lit` package.
import { } from "npm:@use-gpu/webgpu";

@customElement('open-rct3-game')
// deno-lint-ignore no-unused-vars
class Game extends LitElement {
  // See https://lit.dev/docs/components/styles
  static styles = css`
canvas#game {
  position: absolute;
  top: 0;
  width: 100%;
  height: 100vh;
  z-index: 1;
}
  `;

  render() {
    return html`<canvas id="game"></canvas>`;
  }
}

export default <open-rct3-game></open-rct3-game>
