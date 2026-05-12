import { Size, SizeHint, Webview } from "@webview/webview";
import { unimplemented } from "@std/assert";

export const defaultSize: Size = { width: 800, height: 450, hint: SizeHint.MIN };

export default abstract class Window {
  constructor(protected readonly handle: Deno.PointerValue) { }

  abstract title: string;
  abstract size: Size;
  abstract readonly dpi: number;
}

export class WebView extends Window {
  readonly view: Webview;

  constructor(title: string, size?: Size) {
    const view = new Webview(true, size ?? defaultSize);
    super(view.unsafeWindowHandle);
    this.view = view;
    this.view.title = title;
  }

  get title() { return this.view.title; }
  set title(value: string) { this.view.title = value; }

  get size() { return this.view.size; }
  set size(value: Size) { this.view.size = value; }

  get dpi(): number {
    switch (Deno.build.os) {
      // TODO: case "darwin":
      // TODO: case "windows":
      // TODO: case "linux":
      default: {
        unimplemented();
        throw new Error("Unsupported platform!");
      }
    }
  }
}
