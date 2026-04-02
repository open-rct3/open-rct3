# Plan: Allow Extra OVL Files for Local Testing

## Problem
The OVL unit tests currently embed OVL files as embedded resources in the test assembly. Users need to test against additional OVL files from their local RCT3 installation without committing them to source control. CI should not use these extra OVLs.

Also: Remove `Water.common.ovl` from source control (it was likely extracted from RCT3) and reference it from local config instead.

## Solution: Dual-Mode Test Fixture

### Architecture

1. **Configuration File**: Create `OpenCobra/OVL Tests/ovl-tests.local.json` (add to `.gitignore`)
   ```json
   {
     "extraOvls": {
       "Water": "Z:/Games/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets/Water/Water.*.ovl"
     }
   }
   ```

2. **Modified Test Loader**: Update `ReadArchives.cs` to:
   - First, always load embedded resources (source-controlled OVLs)
   - Second, if `ovl-tests.local.json` exists, load additional OVLs from the specified paths
   - Combine both sources for test data

3. **CI Unaffected**: CI environment won't have the local config file, so tests run with only embedded resources

### Implementation Steps

1. Remove `Water.common.ovl` from Git history (to completely remove the file):
   ```bash
   git filter-branch --index-filter "git rm --cached --ignore-unmatch OpenCobra/OVL Tests/Water.common.ovl" --tag-filter-filter "git tag -l" -- --all
   # Or use: git rm --cached OpenCobra/OVL Tests/Water.common.ovl
   ```
   Then delete the file from the working directory.

2. Remove `Water.common.ovl` from `OVL Tests.csproj` (the EmbeddedResource entry)

3. Add `ovl-tests.local.json` to `.gitignore`

4. Create `OpenCobra/OVL Tests/ovl-tests.local.json.example` as a template:
   ```json
   {
     "extraOvls": {
       "Water": "Z:/Games/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets/Water/Water.*.ovl"
     }
   }
   ```

5. Modify test fixtures to support dual-mode loading:
   - Create a new `OvlTestSource` class that combines embedded + local OVLs
   - Update `ReadArchives.Archives` to use this source

6. Document usage in the example config file

### Files to Modify
- Git: Remove Water.common.ovl from history and working directory
- `OpenCobra/OVL Tests/OVL Tests.csproj` - remove Water.common.ovl entry
- `.gitignore` - add `ovl-tests.local.json`
- `OpenCobra/OVL Tests/ovl-tests.local.json.example` - create template
- `OpenCobra/OVL Tests/ReadArchives.cs` - modify to support local OVLs
- Possibly create new helper class for loading

### Alternative Approaches Considered
- Environment variable-based paths (less explicit)
- Command-line arguments (requires CI changes)
- External folder scanning (more complex)

The config file approach is cleanest and requires no CI modifications.