# fx
![image](https://github.com/user-attachments/assets/03305e84-de8b-4ee4-916c-2ca00481094c)

fx is my custom Terminal-based file explorer and multi-tool intended to replace `explorer.exe` for everyday usage.

# Tabs
- [Home]
- [Expl]orer
- [Find] in Files
- [Edit] text
# Controls
## Common
- `>`: Next tab
- `<`: Previous tab
- `Del`: Close tab
## [Home]
### Quick Access
- `/`: Context menu
- `Enter`: Explore dir / Open file
## [Expl]ore
### Quick Access
- `Enter`: Explore dir / Open file
### Path pane
- `/`: Context menu (selected item)
- `:`: Focus on Term bar
- `.`: Context menu (CWD)
- `;`: Mark selected item
- `,`: Recall (sets CWD to dir from Remember)
- `"`: Preview (selected item)
- `?`: Properties (selected item)
### Term bar
- `Backspace` (when empty): Focus on previous pane
## [Edit]
### Text pane
- `Enter` (READ mode): set EDIT mode.
- `Shift+Enter` (EDIT mode): set READ mode.
- `/` (READ mode): Context menu
# Configuration
- Commands.yaml: Provide quick commands for directories and files
- Executables.yaml: Define aliases to external program paths for use in `Commands.yaml`
