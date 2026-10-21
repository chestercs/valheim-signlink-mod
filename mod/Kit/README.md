# Kit: the window kit (`PetyusPolisz.Kit`)

The IMGUI parts every window of our Valheim mods shares: one look, one way to lay out a row, one frame. It holds no
SignLink type: another mod compiles the same folder (`<Compile Include="../Kit/**/*.cs" LinkBase="Kit" />` in its
csproj, or a copy of the folder) and gets the same window. Everything is `internal`, so two mods built with it never
clash.

| File | What it is |
|---|---|
| `KitWindow.cs` | The frame: size from the settings and the screen (`WindowSizing`), the text scale and the look (`Theme.Ensure`), the place it was dragged to, every edge and corner resizing it, the title line, the top part, the body's scroll, the footer, the close row with its keys (and the mod's own part at its left), and clicks' work at a later Layout (`IDeferred`). The mod gives it an `IWindowLook` (its config). |
| `IDeferred.cs` | `Later(action)`: state changes that change what is drawn run at the next Layout (IMGUI draws every event twice: Layout, then the event). |
| `Theme.cs` | The styles (window, sections, labels, buttons, chips, key caps, warnings), built for the text scale: the flat base a look paints over. |
| `Theme.Looks.cs` | The look (1.1.0): `PaintLook` paints one set of shapes over the base in a `Palette` (`Ensure(scale, opacity, "Classic" or "Nordic")`): framed tracks, armed red, round meter, the scrollbar skin, the check box, the game's Averia Serif Libre; the `Painter`; the flat base stays if painting fails. |
| `Theme.Nordic.cs`, `Theme.Classic.cs` | The two palettes (Nordic: slate and amber; Classic: dark wood and gold). |
| `Paint.cs` | The painter behind the look's textures: rounded boxes, frames, clipped bands, a tick, a diamond, a triangle (the group headings' arrow), antialiased (tested in `SignLink.Tests`). |
| `Lang.cs` | The window's words in the player's language (1.1.0): `Lang.T("Close")`, `Lang.F("Loading {0} ...", name)`, `Lang.C(context, english)` for a word that means two things, `Lang.Comma` between a list's items, `Lang.Name` a language by its own name (a table's `@language` line); English is the key and the fallback; a table is `english<TAB>translation` lines; the mod hands in its tables (`Add`), their order in the list (`Order`) and says where the setting and the game's language come from (`Setting`, `Game`); `Lang.Choices` is the setting's list (*Game default*, English, then the tables), and *Game default* takes the game's language. Tested in `SignLink.Tests`. |
| `Widgets.cs` | Sizes and layout: `U`, `W`, `H`, `ContentWidth`, `LabelW` / `RestW`, `Horizontal` / `Vertical` / `Scroll` scopes that close themselves, keyboard focus. |
| `Widgets.Buttons.cs` | Fitted buttons, `Toolbar`, `Tabs`, `TabButton` (a button in the tabs' look), `NextButton`, `Foldout` and `FoldHead` (a group's heading whose arrow turns on hover, 1.1.0), `Check`, `Row`, and the settings rows `Choice` (label + chips) and `OnOff`. |
| `Widgets.Dropdown.cs` | `Dropdown` / `DropChoice` (1.1.0): a button with the choice; its list opens over the window, drawn last (`DropList`, from `KitWindow.EndDraw`) and taking the clicks first (`DropInput`, from `KitWindow.BeginDraw`). |
| `Widgets.Text.cs` | `Header` / `SubHeader` with hotkeys at the right (`Keys`, level with the title), `KeysHere` (level with the middle of a control in the given style, right where the row has got to), `KeyCapButtons` (a hotkey whose keys are its buttons), `Hint`, `Field` (a label and its value; `labelW` for a table with longer labels). |
| `Widgets.Number.cs` | `Number`: a label, a typed field and a slider; Shift + wheel a fine step (`FineStep`); `SizeRows`. |
| `Widgets.Lists.cs` | `PictureItem` (a thumbnail and its label), `EmptyList`. |
| `Meter.cs` | A fill bar with its words: what is there, what would come added, against a limit (SignLink's Area and GIFs bars). |
| `Armed.cs` | A button that asks twice ("Sure? Click again") for what cannot be undone. |
| `FineStep.cs`, `WindowSizing.cs`, `HotkeyText.cs`, `TextHistory.cs`, `ListRows.cs` | The maths and state behind them (tested in `SignLink.Tests`). |

A window in another mod:

```csharp
sealed class MyWindow : MonoBehaviour
{
    KitWindow kit;
    bool open;
    GUI.WindowFunction draw;                    // the delegates made once: none per event
    Action drawBody, drawFooter;
    static readonly string[] CloseKeys = { "Esc" };
    void Awake()
    {
        kit = new KitWindow(0x1234, new MyLook(), Logger.LogInfo, Logger.LogWarning);
        draw = Draw;
        drawBody = DrawBody;
        drawFooter = DrawFooter;
    }
    void OnGUI()
    {
        if (!open) return;
        if (Event.current.type == EventType.Layout) kit.RunLater(false, e => Logger.LogWarning(e));
        kit.Layout();
        kit.Show(draw);
    }
    void Draw(int id)
    {
        kit.BeginDraw();
        kit.TitleBar("MyMod", "1.0.0", null, false);
        kit.BeginTop(); /* tabs */ kit.EndTop();
        kit.Body(drawBody);
        kit.Footer(0, drawFooter);
        if (kit.CloseRow(CloseKeys)) kit.Later(() => open = false);
        kit.EndDraw();
    }
    void DrawBody() { /* rows */ }
    void DrawFooter() { /* the main button, a status line */ }
}
```

Rules the parts keep (the whys are in SignLink's history and `docs/mod-standards.md`):

- Every size is in logical units (the window as designed at 1080p) and goes through `U`: text is drawn at its pixel
  size, never magnified, so it stays crisp at 1440p and 4K.
- A control that appears or vanishes changes only at a Layout (`IDeferred.Later`, or state read once a frame);
  a Layout and its Repaint must draw the same controls.
- No allocation per event in a hot path: labels, options and wordings are cached.
- Words are short: a label says what a control is; a grey line under a row is a word or two, or what stops a button.
  Every hotkey shows where it works (`Keys`).
