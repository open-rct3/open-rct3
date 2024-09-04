import { customElement, LitElement, h, html } from './lit.ts';
// See https://usegpu.live/docs/reference-live-@use-gpu-live
// TODO: Contribute back these lit-html abstractions back to @use-gpu, i.e. a new `@use-gpu/lit` package.
import { } from "npm:@use-gpu/webgpu";

@customElement('open-rct3-game')
class Game extends LitElement {
  render() {
    return html`<canvas></canvas>`;
  }
}

export default <open-rct3-game></open-rct3-game>
