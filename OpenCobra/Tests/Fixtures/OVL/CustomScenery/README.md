# Custom scenery fixtures

Small, real custom-scenery OVL pairs used to exercise `tex`/`flic`/`btbl` (and, where
available, `mms`/`prt`/`psi`/`fct`) parsing against real-world archives without needing
the full RCT3 install. See `.agents/plans/fix/ovl-texture-decoding.md` for the bugs this is
meant to help pin down.

## Adding a pack

1. Prefer small, simple packs (single items or small sets) over mega-packs — these are
   test fixtures, not a scenery library. A few hundred KB to a few MB is plenty.
2. Create a subfolder here named after the pack, containing:
   - `<name>.common.ovl` and, if present, `<name>.unique.ovl` (same base name, so
     `Ovl.Load`'s common/unique pairing finds both).
   - A `README.md` recording:
     - Pack name and original author (and reposter, if different from the original
       creator).
     - The exact source URL(s) you downloaded it from — required so provenance is
       traceable if a creator ever asks about their work being here.
     - The download date.
     - Any license/usage terms stated on the source page, or "none stated" if the page
       didn't say — most RCT3-era community content predates formal licensing and is
       shared informally with attribution rather than under a stated license.
3. No project-file changes needed — `Tests.csproj` embeds everything under this directory
   via a glob, and `TexturesTests.cs` discovers fixtures by resource name.

## Why custom scenery instead of more base-game files

Base-game OVLs (`Fixtures/OVL/BaseGame/`) are Frontier Developments' copyrighted data;
this repo already carries a couple of those pre-existing fixtures, but growing that set
further isn't the right way to get broader parsing coverage. Fan-made custom scenery is
freely distributed by its creators for the RCT3 community, making it the better source
for additional test data — as long as it's attributed here.
