# Park save test fixtures

Real saved-park `.dat` files (the non-OVL DAT container format read by
[`OpenCobra.Data.Dat`](../../../Data/DAT.cs)), embedded into the `Tests` assembly so DAT parsing
can be exercised against real-world saves without needing a full RCT3 install.

- One subfolder per park, containing the `.dat` file plus a `README.md` recording:
  - Park name and original author, if known.
  - The exact source URL you downloaded it from.
  - The download date.
  - Any license/usage terms stated on the source page, or "none stated" if the page didn't say.

Any `.dat` dropped under this tree is picked up automatically by `Tests.csproj`'s
`EmbeddedResource` glob — no project-file edits needed.
