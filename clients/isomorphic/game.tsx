import { css, customElement, LitElement, html } from './lit.ts';
// See https://usegpu.live/docs/reference-live-@use-gpu-live
import { type LC, hot, useFiber } from '@use-gpu/live';
// TODO: Contribute back my lit-html abstractions back to @use-gpu, i.e. a new `@use-gpu/lit` package.
import React from '@use-gpu/live';
import { AutoCanvas, ErrorRenderer, WebGPU } from "@use-gpu/webgpu";

@customElement('open-rct3')
class Game extends LitElement {
  static styles = css`
canvas#game {
  position: absolute;
  top: 0;
  width: 100%;
  height: 100vh;
  z-index: 1;
}
  `;

  /** @returns The viewport's clear color, black. */
  static get clearColor(): GPUColor {
    return [0];
  }
  static get canvas() {
    return document.querySelector("canvas#game")! as HTMLCanvasElement;
  }

  static fallback(err: Error): ReturnType<ErrorRenderer> {
    // TODO: Surface a human-readable error to the user via a toast alert
    console.error(err);
  }

  render() {
    return html`<canvas id="game"><slot></slot></canvas>`;
  }
}

export default <open-rct3>
  <WebGPU fallback={Game.fallback}>
    <AutoCanvas canvas={Game.canvas} backgroundColor={Game.clearColor}></AutoCanvas>
  </WebGPU>
</open-rct3>
