# Napkin Runbook

## Execution and Validation (Highest Priority)
1. **[2026-10-26] Validate without activating driving**
   Do instead: run `dotnet run --project MouseDrive.Tests -c Release` for binding/profile regressions and `dotnet build MouseDrive/MouseDrive.csproj -c Release` for the app; keep automated app runs in standby because driving hides and clips the system cursor.

## Shell and Command Reliability
1. **[2026-10-26] Read source and documentation as UTF-8**
   Do instead: pass `-Encoding utf8` to PowerShell `Get-Content` so Unicode UI labels are not mistaken for source corruption.

## Domain Behavior Guardrails
1. **[2026-10-26] Review binding changes together with release handling**
   Do instead: check capture cancellation, held-button release, and shared output-button state whenever changing key bindings or profiles.
2. **[2026-10-26] Separate pedal previews from game output**
   Do instead: keep standby output at released force while allowing visual input previews; use elapsed seconds for ramps and reset held pedal state on driving transitions, profile switches and edits.
