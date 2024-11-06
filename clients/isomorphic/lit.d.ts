// See https://stackoverflow.com/a/78688588/1363247
declare namespace JSX {
  interface IntrinsicElements {
    // QUESTION: Make this more specific?
    [tag: string]: any
  }
}
