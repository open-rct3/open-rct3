# Plan: Excitement, Intensity, Nausea (EIN) Rating Model

**Roadmap**: Unscheduled (Spun off from [ride-track-spline.md](ride-track-spline.md) common `Ride` base
type work)

> [!IMPORTANT]
> **Blocked on foundational work.** EIN calculation requires a queryable track data model and stable ride
> instance representation. See [Rides & Track Splines](../../TODO.md#rides--track-splines) in `TODO.md` for the
> prioritized dependency chain: bank propagation fix → world-space rendering → track editor → content
> authoring. Physics-based intensity/nausea calculations are further blocked on a physics simulation layer
> (currently out of scope).

## Context

RCT's EIN formulas were never officially documented by the original developers; the values used across the
series are inconsistent between games. Community reverse-engineering efforts (fan wiki writeups, decompiled
formula approximations) exist but are inference/deduction, not a confirmed spec. Treat any formula sourced
from these as a starting approximation, not ground truth.

## Goals

- Define `Excitement`, `Intensity`, `Nausea` as rating fields on the common `Ride` base type.
- Decide formula inputs: likely derived from track shape data (max speed, max G-force, air time, inversions,
  drop height, duration) once the track-spline data model exists to query them.
- Decide whether ratings are computed once (on ride construction/test) and cached, or recomputed live as track
  is edited.

## Open Questions

- **Formula baseline**: Which community-sourced approximation (if any) to adopt vs. designing an original
  formula tuned to this engine's physics model.
- **Physics dependency**: Can excitement be calculated from pure geometry (drop height, inversions, duration)?
  Or does intensity/nausea require a working physics simulation layer for G-force/speed? Currently, only
  geometry-based excitement is viable; intensity/nausea are blocked until physics is in place (separate scope,
  not part of this plan or track-spline plan).

## Status

Stub only — not started, no further work planned until picked up explicitly.
