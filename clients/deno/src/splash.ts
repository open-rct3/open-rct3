import { css, html } from "lit";

export const styles = css`html, body {
  margin: 0;
  padding: 0;
}

body {
  /* See https://css-tricks.com/snippets/css/system-font-stack/ */
  font-family: system-ui, "Segoe UI", Roboto, Oxygen-Sans, Ubuntu, Cantarell, "Helvetica Neue", Helvetica, Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol";
  display: flex;
  flex-direction: column;
}

header {
  display: flex;
  align-items: center;
  margin-inline: 8px;
}
header h1 {
  flex: 1;
  font-size: 24px;
  font-weight: 400;
  margin-inline-start: 8px;
}

main {
  flex: 1;
  margin: 0 auto;
}

/* See */

.throbber.ripple,
.throbber.ripple div {
  box-sizing: border-box;
}
.throbber.ripple {
  color: black;
  display: inline-block;
  position: relative;
  width: 80px;
  height: 80px;
}
.throbber.ripple div {
  position: absolute;
  border: 4px solid currentColor;
  opacity: 1;
  border-radius: 50%;
  animation: lds-ripple 1s cubic-bezier(0, 0.2, 0.8, 1) infinite;
}
.throbber.ripple div:nth-child(2) {
  animation-delay: -0.5s;
}
@keyframes lds-ripple {
  0% {
    top: 36px;
    left: 36px;
    width: 8px;
    height: 8px;
    opacity: 0;
  }
  4.9% {
    top: 36px;
    left: 36px;
    width: 8px;
    height: 8px;
    opacity: 0;
  }
  5% {
    top: 36px;
    left: 36px;
    width: 8px;
    height: 8px;
    opacity: 1;
  }
  100% {
    top: 0;
    left: 0;
    width: 80px;
    height: 80px;
    opacity: 0;
  }
}
`;

export const splash = html`<header>
  <img src="https://rct3.chancesnow.me/logo.png" alt="" />
  <h1>OpenRCT3</h1>
  <div class="throbber ripple" title="Loading…"><div></div><div></div></div>
</header>
<main>
</main>
`;
