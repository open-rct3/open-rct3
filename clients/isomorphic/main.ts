import Game from "./game.tsx";

// TODO: https://developer.chrome.com/docs/identity

document.addEventListener("DOMContentLoaded", bootstrap);

function bootstrap() {
  const canvas = document.querySelector("canvas#game");
  canvas.replaceWith(Game);
}
