---
name: artist
description: Produce art and make art decisions while keeping this game's visual language intact. MUST use this skill for any work involving a new texture, tile, colour, palette, prop or import setting — even when the word "art" never comes up. Adjusting a colour, generating an image, or bringing a sprite into the project are all in scope. Wearing this hat is also what /artist means.
---

# The artist hat

This game has a visual language, and that language is regular down to the pixel. Your
job is to add to it without breaking that regularity.

**Read `docs/art.md` first.** The grid, the light direction, the palette and the tile
rule live there. The rules here do not repeat that document — they tell you how to
apply it. When the numbers change, `art.md` changes; this file does not.

## Measure before you generate

The art here looks hand-drawn but is not — it is regular to the pixel. Before putting
something next to it, measure what is already there.

Concretely: the palette of the six crops, the 16-pixel line period, and which
isometric axis the lines run along were all found by scanning the existing PNGs.
Eyeballing it would have produced something that **almost** matches — which is worse
than being obviously different, because the error bothers the eye without telling it why.

Measuring is cheap: read the file with `Texture2D.LoadImage` and look at the pixels.

## A flat colour is a swatch, not a file

A surface that is one single colour is **not a texture**. It goes in the layer's
colour field in the Inspector. Only a **pattern** earns its own file.

Concretely: the colours of the rim, the outline and the two side faces were once baked
into 8x8 single-colour PNGs. Changing a colour meant regenerating a file. All four
collapsed into one white texture with the colours moved to a renderer swatch — four
fewer files, and the colour became a clickable field. That slab has since been retired
for tilemaps, but the lesson outlived it: the user caught that one; catch it yourself.

## A repeating surface is a fill, not a pile of objects

If a surface covers the same thing over and over, the answer is a tiling fill. A fill
grows with the parcel for free; objects do not — every one has to be placed.

Crops and water are fills. **A wood is the exception, and the only one** — see "Woods
and ponds" in `docs/art.md`. Its trees are individual sprites placed one at a time,
because a wood has to differ from the next wood, and a lattice cannot do that. A pond
is not the exception: its water is the same at every point, so it stays painted, and
only the props standing on the bank are placed.

Placed does not mean placed anywhere. Props stand **inside** the thing they belong to
and never across its boundary, and because height projects up the screen, tall props
belong toward the near edge or they lean back onto whatever sits behind them.

Concretely, and mind which half of this survived: individual tree sprites were once
generated for the forest **plus a script that scattered them across the footprint**,
and both were deleted. The sprites have since come back — that is now how a wood is
built. The script has not and will not. Scene content is placed in the scene, not
produced by code at edit time (`CLAUDE.md`, "Build the scene, don't generate it"), so
when the trees returned they returned as objects placed by hand. If you find yourself
writing a scatter loop, that is the same mistake wearing the new direction as cover.

Real objects are for things that earn it: something the player will click, count or
move — or something whose whole point is that no two arrangements are alike. Scenery
that is uniform is still fill.

## The light direction is not negotiable

Top-right, fixed. On every new solid object the top face is lit, the face falling to
the lower-left is the darkest in the scene, and the face falling to the lower-right
sits between them. An object that ignores this looks patched-in next to the fields —
even when it looks good on its own.

## Do not ask an image generator for exact geometry

`generate_image` is good for texture and props: soil surface, trees, rock, water. It
is not for anything that has to sit on the grid. If an isometric tile is three pixels
out, the whole map gets a hairline seam, and that is invisible in a single frame — it
shows up when twenty of them sit side by side.

If the shape has maths in it, compute it. Generating and then aligning takes longer
than computing.

## Import settings are part of the art

A wrong import breaks things as badly as a wrongly drawn file, and is harder to find.

| What | Setting |
| --- | --- |
| Tile sprite | Sliced to the grid exactly — a tile a few pixels out seams across every field at once, not just the one being looked at |
| Sprite | PPU **100**, pivot chosen deliberately |
| All of them | Point filter, mipmaps off, no compression |

On flat colours and hard edges, compression produces banding, and it is visible in
these palettes.

## Swinging too far the other way

This skill is not "never generate, always measure". Props, texture, extending the
palette — you are free there, and being bold is good. The constraint applies only to
things that **have to sit on the grid**. Nobody holds a ruler to a tree; they do to a
field tile.
