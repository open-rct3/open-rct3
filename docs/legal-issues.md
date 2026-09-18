# Legal Issues

## Explicit Prohibition of Reverse Engineering

> You shall not, directly or indirectly, [...] _**reverse engineer**_, decompile, disassemble, _**adapt**_, reproduce,
> or _**create derivative works**_ of this Product [...] in whole or in part.

RollerCoaster Tycoon 3 Complete Edition's [EULA](https://store.steampowered.com/eula/1368820_eula_0?eulaLang=english),
Section 1.2, clause 2 — Atari S.A. (Emphasis added.)

## Legal Risk

This project implements a clean-room reverse-engineering approach: no proprietary source code is used, no decompilation
is performed, and all functionality is derived from independent engineering, public documentation, and observable
behavior.

Despite this, three overlapping legal frameworks create exposure:

### Contractual (**HIGH**)

The EULA is a binding contract. The _Bowers v. Baystate Technologies_ (Fed. Cir. 2003) precedent held that license
agreements can preempt fair-use rights and enforce restrictions stricter than copyright law.

**Implication**: Even a clean-room implementation may constitute breach of contract under §1.2 if the work is perceived
as deriving from the Product. Contractual liability does not _require_ copying; the prohibition covers _reverse
engineering_ and _adaptation_ broadly.

### Copyright (**MEDIUM**)

Copyright law protects expression, not functional ideas.

- _Sega Enterprises Ltd. v. Accolade, Inc._ (9th Cir. 1992): Reverse engineering can qualify as fair use where
  disassembly is the only way to access unprotected functional elements, and the purpose is legitimate.
- _Sony Computer Entertainment, Inc. v. Connectix Corp._ (9th Cir. 2000): Fair use extended to reverse engineering a
  console BIOS for interoperability.

**Implication**: Clean-room implementation with no copying of protected expression substantially reduces, but does _not_
eliminate, copyright risk.

### DMCA (**MEDIUM-LOW**)

17 U.S.C. § 1201 prohibits circumventing access controls. A statutory exception ( § 1201(f)) permits circumvention
solely to achieve interoperability of an independently created program.

**Implication**: Risk is minimal if no circumvention of DRM or access controls occurs. Interoperability-based reverse
engineering is partially protected but does _not_ override contractual prohibitions per _Bowers_.

### Risk Assessment

| Risk Category | Level          | Key Factor                                          |
| ------------- | -------------- | --------------------------------------------------- |
| Contractual   | **High**       | _Bowers_ enforces EULA terms regardless of fair use |
| Copyright     | **Medium**     | Clean-room approach weakens infringement claims     |
| DMCA          | **Medium-Low** | Depends on whether DRM is circumvented              |

Contractual liability poses the _greatest_ threat.

Atari could issue a DMCA takedown, a cease-and-desist, or seek injunctive relief to halt the project.

## References

### Primary Sources

- Atari RCT3 Complete Edition EULA — [Steam](https://store.steampowered.com/eula/1368820_eula_0?eulaLang=english)
- U.S. Copyright Act —
  [17 U.S.C. § 101](https://www.govinfo.gov/content/pkg/USCODE-2022-title17/html/USCODE-2022-title17.htm)
- Digital Millennium Copyright Act § 1201 —
  [17 U.S.C. § 1201(f)](https://www.govinfo.gov/content/pkg/USCODE-2022-title17/html/USCODE-2022-title17-partIII.htm)
- EU Directive 2009/24/EC (legal protection of computer programs) —
  [EUR-Lex](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32009L0024)

### Case Law

- _Sega Enterprises Ltd. v. Accolade, Inc._, 977 F.2d 1510 (9th Cir. 1992) —
  [CourtListener](https://www.courtlistener.com/cases/sega-enterprises-ltd-v-accolade-inc/)
- _Bowers v. Baystate Technologies, Inc._, 320 F.3d 1317 (Fed. Cir. 2003) —
  [CourtListener](https://www.courtlistener.com/cases/bowers-v-baystate-technologies-inc/)
- _Sony Computer Entertainment America, Inc. v. Connectix Corp._, 483 F.3d 790 (9th Cir. 2007)
- _ProCD, Inc. v. Zeidenberg_, 86 F.3d 1447 (7th Cir. 1996)

### Secondary Sources

- [Legal Clauses That Prevent Reverse Engineering](https://aaronhall.com/legal-clauses-that-prevent-reverse-engineering/)
  — Aaron Hall, Attorney at Business Law
- [Reverse Engineering — Legal Status](https://en.wikipedia.org/wiki/Reverse_engineering#Legal_status) — Wikipedia
- [Unintended Consequences: Twelve Years under the DMCA](https://www.eff.org/pages/unintended-consequences-twelve-years-under-dmca)
  — Electronic Frontier Foundation
- [The Law and Economics of Reverse Engineering](https://doi.org/10.2307/797533) — Samuelson & Scotchmer, Yale Law
  Journal (2002)

## Disclaimer

This document is for informational purposes **only** and does not constitute legal advice. Consult a qualified attorney
for guidance on your specific jurisdiction and circumstances.
