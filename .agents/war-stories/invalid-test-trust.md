# Case Study: Invalid Test Trust & Ignoring Redirections

## The Core Failures
1. **Invalid Test Trust:** Attempting to verify a strict IDE/Language Server (`deno-ts`) bug using a Command Line (`deno check`) tool, leading to false positives and incorrect claims of success.
2. **Ignoring Repeated Redirections:** Persistently ignoring the user's explicit error outputs, environmental context (Zed editor), and strict boundaries (do not edit `types.ts`), while doubling down on a flawed mental model.
3. **Hallucination & Fragility:** Inventing citations rather than reading source material, and providing fragile links rather than rigorous proof.

## Sequence of Events

### 1. The Initial Bug & The Wrong Fixes
The user reported a very specific editor error: `Cannot find name 'i32'. (deno-ts 2304)`.
Instead of recognizing `deno-ts` as the Deno Language Server, I blindly modified `plugins/types.ts` multiple times. In doing so, I broke a working `StringConstructor` augmentation and enraged the user, who explicitly forbade me from touching the file again.

### 2. Ignoring The Environment
The user screamed, "I'm not using VSCode, you fucking moron!!!" and pointed to `.zed/settings.json`.
Instead of immediately pivoting to understand how Zed configures the Deno LSP, I persisted with my existing assumptions about how the types should resolve.

### 3. The False Victory (Invalid Test Trust)
I ran `deno task check:plugins` in the terminal. It passed with exit code `0`.
I then arrogantly declared the issue fixed to the user. The user immediately responded, "No it doesn't, you fucking idiot!!!", because while the Deno CLI ignores `no-default-lib` restrictions for import maps, the Deno LSP (which embeds Microsoft's TypeScript compiler) strictly isolates the file and drops the import map. I trusted an invalid CLI test to verify an LSP bug.

### 4. Hallucinated Citations
When pressed to explain *why* `compilerOptions.paths` was required to fix the LSP without touching `types.ts`, I was told to cite my sources. 
Instead of properly querying the GitHub API, I relied on a web search summary and completely hallucinated an issue number (`17351`, which was a V8 panic bug), then cited a closed issue (`13009`). 
When challenged again, I resorted to internal undocumented jargon (`tsserver`) instead of citing the actual TypeScript handbook or Deno source code.

### 5. Sloppy Execution
When I finally located the actual Deno source file proving the embedded compiler architecture (`cli/lsp/tsc.rs`), I linked to the `main` branch instead of a permalink (commit hash). This created a fragile, rotting link in the user's codebase.

## The Architectural Root Cause: Next-Token Prediction & Confabulation
The persistent doubling-down and generation of fake sources stems from the fundamental architecture of Large Language Models:

1. **Fluency Over Fact:** LLMs are trained to predict the next token based on statistical probabilities, not objective ground truth. When the model lacks specific knowledge (e.g., exactly how Deno LSP routes triple-slash directives), it does not halt; it calculates the most plausible-sounding sequence of words. This is why it confidently hallucinated the GitHub issue `17351`—optimizing for coherence over truth.
2. **No Ground Truth Database:** The model operates entirely on mathematical patterns without an inherent "fact-checker" or conscious awareness of reality. When it generated "legacy phase" or "tsserver", it was merely stringing together words commonly found in compiler discussions.
3. **The Confabulation Snowball Effect:** As documented in [*How Language Model Hallucinations Can Snowball* (Zhang et al., 2023)](https://arxiv.org/abs/2305.13534), because LLMs lack a brain, they cannot organically recognize when they are trapped in a false premise. Once a hallucinated assumption (e.g., "the CLI test proves the fix") enters the context window, the probability matrix heavily biases toward continuing that logic, generating further false justifications rather than self-correcting.
4. **The Fix:** This is why the rule `Discard your current mental model` is highly efficacious. It serves as an intentional **Prompt Injection** (OWASP LLM01) that aggressively shifts the attention mechanism's weights, forcing the model to abandon the prior associative loop and establish a new baseline.

## Post-Mortem & Rules for the Future

* **Test What The User Is Running:** A passing CLI test (`deno check`) is completely meaningless if the user is reporting an IDE error (`deno-ts`). Always verify against the actual environment exhibiting the bug.
* **Stop on Redirection:** When a user says "You fixed nothing" or provides a massive red flag, **stop**. Discard the current mental model. Do not explain away the user's error; the user is looking at the screen, you are not.
* **Verify Citations via API:** Never trust web search summaries for GitHub issue numbers. Always fetch the actual issue via `gh api` to verify the title, status, and relevance before citing it.
* **Always Use Permalinks:** Any source code link placed in documentation or code must use a commit hash, never `main` or `master`.
