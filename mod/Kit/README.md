# Kit: the window kit (`PetyusPolisz.Kit`)

The IMGUI parts every window of our Valheim mods shares: one look, one way to lay out a row, one frame. It holds no
SignLink type: another mod compiles the same folder (`<Compile Include="../Kit/**/*.cs" LinkBase="Kit" />` in its
csproj, or a copy of the folder) and gets the same window. Everything is `internal`, so two mods built with it never
clash.

| File | What it is |
|---|---|
| `KitWindow.cs` | The frame: size from the settings and the screen (`WindowSizing`), the text scale and the look (`Theme.Ensure`), the place it was dragged to, every edge and corner resizing it, the title line, the top part, the body's scroll, the footer, the close row with its keys, and clicks' work at a later Layout (`IDeferred`). The mod gives it an `IWindowLook` (its config). |
| `IDeferred.cs` | `Later(action)`: state changes that change what is drawn run at the next Layout (IMGUI draws every event twice: Layout, then the event). |
| `Theme.cs` | The styles (window, sections, labels, buttons, chips, key caps, warnings), built for the text scale. |
| `Widgets.cs` | Sizes and layout: `U`, `W`, `H`, `ContentWidth`, `LabelW` / `RestW`, `Horizontal` / `Vertical` / `Scroll` scopes that close themselves, keyboard focus. |
| `Widgets.Buttons.cs` | Fitted buttons, `Toolbar`, `Tabs`, `NextButton`, `Foldout`, `Check`, `Row`, and the settings rows `Choice` (label + chips) and `OnOff`. |
| `Widgets.Text.cs` | `Header` / `SubHeader` with hotkeys at the right (`Keys`, level with the title), `KeyCaps`, `Hint`, `Field`. |
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
    void Awake() => kit = new KitWindow(0x1234, new MyLook(), Logger.LogInfo, Logger.LogWarning);
    void OnGUI()
    {
        if (Event.current.type == EventType.Layout) kit.RunLater(false, e => Logger.LogWarning(e));
        kit.Layout();
        kit.Show(Draw);
    }
    void Draw(int id)
    {
        kit.BeginDraw();
        kit.TitleBar("MyMod", "1.0.0", null, false);
        kit.BeginTop(); /* tabs */ kit.EndTop();
        kit.Body(DrawBody);                 // a method, made once: no delegate an event
        kit.Footer(0, DrawFooter);
        if (kit.CloseRow(new[] { "Esc" })) open = false;
        kit.EndDraw();
    }
}
```

Rules the parts keep (the whys are in SignLink's history and `docs/mod-standards.md`):

- Every size is in logical units (the window as designed at 1080p) and goes through `U`: text is drawn at its pixel
  size, never magnified, so it stays crisp at 1440p and 4K.
- A control that appears or vanishes changes only at a Layout (`IDeferred.Later`, or state read once a frame);
  a Layout and its Repaint must draw the same controls.
- No allocation an event in a hot path: labels, options and wordings are cached.
- Words are short: a label says what a control is; a grey line under a row is a word or two, or what stops a button.
  Every hotkey shows where it works (`Keys`).
