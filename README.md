# AutomationId Manifest PoC

Proof-of-concept for a single source of truth that keeps WPF/Avalonia
`AutomationProperties.AutomationId` values in lockstep with UI test
code, eliminating a class of test flakiness driven by ID drift.

## What this proves

> XAML files are the source of truth. A codegen step (`tools/AxamlIdGen`)
> extracts every AutomationId, validates uniqueness with template-scope
> awareness, fills in missing IDs from `@auto` sentinels deterministically,
> and emits typed manifests for both .NET and TypeScript test layers.
> Test code references generated constants only — so renaming or removing
> an AutomationId in XAML breaks tests at **compile time**, not at runtime
> as flake.

## Layout

```
SampleApp/                  Avalonia desktop app (cross-platform stand-in for WPF)
  Views/{LoginView,SettingsView}.axaml   Mix of explicit IDs and @auto sentinels
  Generated/AutomationIds.g.cs           Generated; do not edit

tools/AxamlIdGen/           THE reusable production deliverable
  src/Scanning, Synthesis, Validation, Emit, Cli
tests/AxamlIdGen.Tests/     Unit + integration + CLI + property + real-world tests

tests/SampleApp.Headless/   Avalonia.Headless tests proving every manifest entry
                            resolves to a real control in the visual tree

e2e/                        WebdriverIO + Appium scaffold for the eventual
                            Windows-side WPF test execution

manifest/automation-ids.json   Generated language-neutral manifest
e2e/automation-ids.generated.ts Generated TypeScript manifest

axamlidgen.config.json      Tool configuration
```

## Uniqueness creation strategy

Hand-written IDs are sacred — the tool **never** silently renames them.
Three states per control are handled differently:

| State                        | Tool behaviour                                          |
|------------------------------|---------------------------------------------------------|
| Explicit string              | Preserved verbatim. Validated for uniqueness + naming.  |
| `@auto` sentinel             | Replaced in-place with a synthesized deterministic ID.  |
| Missing                      | Reported (strict policy). No XAML mutation.             |

Synthesis prefers, in order: `x:Name` → static `Content`/`Text`/`Header`
→ owning property name → ordinal fallback (warned). Each generated ID
records its provenance in the JSON manifest.

Template scope (`<ControlTemplate>`, `<DataTemplate>`, `<Style.Setter>`)
is detected; AutomationIds inside templates are validated *per template
instance*, not against the global instance pool. This prevents
false-positive duplicate errors on real WPF codebases (see metris-core
verification below).

## Running it

```bash
# 1. Install prerequisites (.NET 10 SDK, Avalonia templates, native libs)
./setup.sh

# 2. Build everything (codegen runs automatically as a BeforeBuild step)
dotnet build AxamlIdPoc.slnx

# 3. Run the full test suite
dotnet test AxamlIdPoc.slnx

# 4. Type-check the WDIO suite against the generated TS manifest
cd e2e && npm install && npx tsc --noEmit
```

CLI surface:

```bash
dotnet run --project tools/AxamlIdGen -- scan  SampleApp --config axamlidgen.config.json
dotnet run --project tools/AxamlIdGen -- write SampleApp --config axamlidgen.config.json
dotnet run --project tools/AxamlIdGen -- check SampleApp --config axamlidgen.config.json
dotnet run --project tools/AxamlIdGen -- emit  SampleApp --config axamlidgen.config.json
```

Exit codes: `0` clean | `1` validation errors | `2` IO/parse errors | `3` manifests stale.

## Verification on this machine

### A. Synthetic / controlled

- `dotnet test` — **77 tests pass** (72 AxamlIdGen + 5 Avalonia.Headless).
- The Headless tests confirm every `instance`-scope manifest entry resolves
  to a real Avalonia control in the visual tree.
- `npx tsc --noEmit` in `e2e/` — TypeScript validates the WDIO suite
  against the generated manifest. Any reference to a non-existent ID is
  a compile error.

### B. Real WPF codebase: `../metris-test/metris-core/`

The `RealWorld/MetrisCoreTests` class runs against the user's actual
WPF tree. Survey numbers from this machine:

- **2,782 XAML files**
- **5 existing AutomationId occurrences** (4 template-scoped, 1 instance)
- **Two duplicates** (`VerticalScrollBar`/`HorizontalScrollBar`) that
  legitimately appear in two `<ControlTemplate TargetType="ScrollViewer">`
  blocks — these MUST NOT be flagged as duplicates.

Confirmed:

| Check                                                          | Result            |
|----------------------------------------------------------------|-------------------|
| Full-tree scan succeeds within 30s budget                      | ~5s               |
| All 5 existing IDs are discovered                              | ✅                |
| ScrollBar duplicates are template-scoped, not flagged          | ✅                |
| Fabricated instance-scope duplicate is caught                  | ✅                |
| Migration simulation (write `@auto` sentinels into a subset)   | ✅ idempotent      |
| `scan` does not modify any source files                        | ✅                |

## Migration to the real WPF target (later)

The codegen tool is dialect-aware: it parses both `.axaml` (Avalonia) and
`.xaml` (WPF / Xamarin.Forms) without configuration changes. To point at
a real WPF project:

1. Replace `axamlidgen.config.json`'s `outputs` paths to land manifests
   inside the WPF project tree.
2. Add a `<Target Name="GenerateAutomationIds" BeforeTargets="BeforeBuild">`
   target to the WPF `.csproj` (mirror `SampleApp/SampleApp.csproj`).
3. Drop the existing `e2e/` scaffold against the new TypeScript manifest;
   set `appium:app` in `wdio.conf.ts` to the built WPF executable.
4. Run `npm test` from `e2e/` on a Windows machine with Appium 2 and
   `appium-windows-driver` installed.

The manifest contract (`AutomationIds.<key>`) and the codegen itself
need no change.

## What's intentionally out of scope

- Live WDIO execution (requires a Windows host with the WPF app deployed).
- CI YAML — README documents what CI must run; pipeline files are project-specific.
- Custom `AutomationPeer` authoring for bespoke controls.
- Fuller property-based testing via FsCheck (current "property" tests use
  randomized seeds with explicit theory cases, sufficient for the PoC).
