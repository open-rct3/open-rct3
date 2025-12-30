import { assert, assertInstanceOf } from "@std/assert";
import { WebGPURenderer } from "four";

import { css, customElement, LitElement, h, html, ref, PropertyValues } from './lit.ts';

@customElement('open-rct3')
// deno-lint-ignore no-unused-vars
class Game extends LitElement {
  readonly #resized = this.resized.bind(this);
  private renderer: WebGPURenderer | null = null;

  static styles = css`
canvas#game {
  position: absolute;
  top: 0;
  width: 100%;
  height: 100vh;
  z-index: 1;
}
  `;

  override connectedCallback(): void {
    super.connectedCallback();
    self.addEventListener("resize", this.#resized);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    self.removeEventListener("resize", this.#resized);
  }

  override render() {
    return html`<canvas id="game" ${ref(el => {
      // TODO: Disconnect the renderer and whatnot from the old canvas
      if (el === undefined) return;

      assertInstanceOf(el, HTMLCanvasElement);
      this.renderer = new WebGPURenderer({ canvas: el });
      this.renderFrame();
    })}></canvas>`;
  }

  private renderFrame() {
    // TODO: Render the game
  }

  private resized() {
    if (!this.renderer) return;
    this.renderer.setSize(this.clientWidth, this.clientHeight);
  }
}

export default <open-rct3></open-rct3>
