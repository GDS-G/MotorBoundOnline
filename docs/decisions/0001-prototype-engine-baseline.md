# Decision 0001: Prototype Engine Baseline

- Status: provisional
- Date: 2026-08-06

## Decision

Use the locally installed Unity `2022.3.62f2` editor for the first engineering prototype. Keep domain and force-model code independent from rendering and avoid APIs that would obstruct migration to the Unity 6 production candidate described by the GDD.

## Reason

The production engine choice is explicitly gated by console, middleware, licensing, render-pipeline, and performance validation. Installing or declaring a different production editor before those checks would falsely close an open design decision. The existing LTS editor is sufficient to compile and measure the early dynamics prototype.

## Exit condition

Before large-scale art production, profile the reference scene on a Unity 6-class LTS candidate and record a continue/migrate decision with platform evidence.
