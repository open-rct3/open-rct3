# Plan: Excitement, Intensity, Nausea (EIN) Rating Model

**Roadmap**: Unscheduled — stub spun off from [ride-track-spline.md](ride-track-spline.md) common `Ride` base
type work.

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

- Which community-sourced formula approximation (if any) to adopt as a baseline vs. designing an original
  formula tuned to this engine's physics model.
- Does intensity/nausea rating require the physics simulation layer (out of scope for the track-spline plan)
  to be in place first, since G-force and speed are physics outputs, not static geometry?

## Status

Stub only — not started, no further work planned until picked up explicitly.
