---
version: alpha
name: Alder.Avalonia
description: The design system for native Avalonia desktop controls and their interactive Gallery.
colors:
  accent: "#0485F7"
  accent-hover: "#3592F9"
  on-accent: "#FCFCFC"
  ink-light: "#18181B"
  ink-dark: "#FCFCFC"
  muted-light: "#71717A"
  muted-dark: "#9F9FA9"
  surface-light: "#FFFFFF"
  surface-dark: "#18181B"
  surface-secondary-light: "#EFEFF0"
  surface-secondary-dark: "#232325"
  neutral-light: "#EBEBEC"
  neutral-dark: "#27272A"
  border-light: "#DDDEE0"
  border-dark: "#28282C"
  separator-light: "#E4E4E7"
  separator-dark: "#212124"
  gallery-preview-light: "#F5F5F5"
  gallery-preview-dark: "#060607"
rounded:
  field: "12px"
  preview: "14px"
  sidebar-row: "16px"
  gallery-example: "20px"
  select-popover: "22px"
  card: "24px"
spacing:
  attached-toolbar-inset: "4px"
  gallery-preview-gap: "8px"
  gallery-preview-padding-x: "24px"
  gallery-preview-padding-y: "20px"
---

## Overview

**English** · [简体中文](DESIGN.zh-CN.md)

Alder is a native desktop control library. `Alder.Avalonia` provides basic controls, layout shells, complex data components, and AI conversation components. The Gallery uses live controls to demonstrate states and composition. Web references guide visual and interaction checks. The application uses C#, AXAML, and Avalonia's input, layout, and rendering.

### References for new components

**Use the React Web components in HeroUI and HeroUI Pro as Alder's references for appearance, interaction, and motion.** Find the documentation and interactive preview before selecting the variants and scenarios to implement. [HeroUI React Web](https://heroui.com/en/docs/react/components) · [HeroUI Pro React Web](https://heroui.pro/docs/react/components)

- **When a corresponding Web component exists**, record the documentation and preview links and exercise the target states before implementation. Check size, spacing, icons, borders, corners, and shadows. Exercise the relevant hover, press, release, focus, open, close, and drag states. Check duration, easing, and reduced motion. Use the same scenarios to verify the Gallery.
- **When there is no Web equivalent**, reuse the semantic resources, states, and motion of the nearest Alder control in purpose and interaction. Record the reference control and the rationale for any new behavior in the commit message. Use task-specific references for the scenarios they cover.

Dimensions in this document are Avalonia DIPs. The `px` values in the front matter are for design document interchange. The Alder theme, each component theme, and the Gallery app specify the runtime values. [AlderTheme.axaml](src/Alder.Avalonia/Themes/AlderTheme.axaml) · [Gallery App.axaml](samples/Alder.Avalonia.Gallery/App.axaml)

## Colors

| Role | Avalonia resources | Use |
| --- | --- | --- |
| Accent | `AlderAccent`, `AlderAccentHover`, `AlderOnAccent` | Primary actions, emphasized selection, and relevant keyboard focus states |
| Text | `AlderInk`, `AlderMuted` | Headings and body text, supporting copy, and secondary icons |
| Surface | `AlderSurface`, `AlderSurfaceSecondary`, `AlderNeutral` | Cards, overlays, secondary layers, and interactive fills |
| Outline | `AlderBorder`, `AlderSeparator` | Container strokes and content dividers |
| Gallery | `GalleryPreviewSurface`, `GalleryCodeSurface` | Backgrounds for previews, example cards, and code areas |

Templates resolve colors and shadows using `DynamicResource`. Verify the same state with the Light and Dark theme dictionaries. Use the relevant template for semantic colors, hover fills, disabled states, and subtle dividers. Start with component-specific resources when available.

## Typography

The Gallery loads Inter through `WithInterFont()`. Control templates inherit fonts. Body text and action labels typically use 14 DIP, and supporting text uses 12 DIP. Check multiline content with its actual `LineHeight` and wrapping. Single-line tables, options, and menus can truncate at limited widths. Keep font, language, and scale consistent during comparisons.

## Layout

Compose controls through content slots, collections, and data templates. A standard Gallery example card leaves 8 DIP between the preview and its shell. Content typically has 24 DIP horizontal and 20 DIP vertical padding. Set margins on the relevant containers for full-bleed or compact previews.

Sidebar and AppLayout choose their layout from the available width. The icon rail retains navigation, the brand icon, and search when enabled. At narrow widths, check nested scrolling, truncated titles, popup flipping, and window edges.

## Elevation & Depth

Fields, cards, and settings cells use short outer shadows. Overlays and attached toolbars use wider shadows and retain a thin inner edge in Dark. Dark shadows use a soft neutral layer near black backgrounds and a wider black layer to add depth against lighter dark surfaces. Shared cards paint the surface at the outer layer and handle dividers and child states independently.

Keep shadows, keyboard rings, and drag previews on layers that allow overflow. Clip rounded content at the smallest relevant layer and use an overlay stroke when needed to cover the inner edge. Sample the background, both sides of the stroke, and the shadow falloff when comparing screenshots.

## Shapes

Fields typically use 12 DIP corners, Gallery previews use 14 DIP, and Sidebar navigation rows often use 16 DIP. Example card shells use 20 DIP, the default Select popup uses 22 DIP, and the default AlderCard uses 24 DIP. Group items, borderless layouts, circular icons, and fine dividers have their own shapes. Variant sizing belongs in the relevant component theme.

## Components

- Build advanced controls from Alder primitives and themes in AXAML and C#. Parts that require templates get them from `AlderTheme`. Verify new or updated advanced controls in a host that loads only `AlderTheme`. Avalonia primitives can provide layout, drawing, and input.
- Use AlderButton for regular actions. Reuse AlderPressableFeedback for press states. Continuous drags follow the pointer. Each component handles its own snap or settle motion.
- For keyboard focus, buttons use the component's hover feedback or a dedicated focus style. Content-area tab navigation skips system buttons in the title bar.
- Keep form surfaces consistent across rest, hover, editing, and validation. Cell shows selection and keyboard focus separately. Selection triggers, option rows, and sliders show focus within their rounded surfaces or thumbs. Text editing and validation retain their respective outlines.
- Host overlays in Alder menus, pickers, dialogs, or Popover. Check first open, content scrolling, focus after dismissal, the rounded inner edge, and the outer shadow.
- Short background and opacity transitions commonly use 150 ms. Some layout, slider position, and expansion transitions use 200 ms. Refer to the component for its exact duration and easing. Interaction tests cover the effective `AlderMotion.Reduced` setting.
- Maintain the component catalog and keep the Gallery's AXAML and C# source in sync with its previews. [Component catalog](docs/COMPONENTS.md)

## Do's and Don'ts

- Start with the related theme, named parts, and existing example. Check affected controls before changing a shared template and reuse existing semantic resources and behavior.
- Keep pointer, keyboard, accessible names, and MVVM state consistent. Give icon buttons accessible names. Preserve the current Gallery page and state when switching themes.
- For clipping regressions, trace the template and its ancestors up to the window or overlay. Record the affected pixels and samples after the fix. Record theme, width, DPI, and interaction phase for visual checks.
- Provide Reset in examples that can remove or hide data. Add new visible copy to both the English and Simplified Chinese Gallery catalogs.
