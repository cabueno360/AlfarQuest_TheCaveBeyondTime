# Retired map generators

These scripts **emitted** the `.tmx` maps under `src/AlfarQuest.Client/wwwroot/Maps/`
— the regions, the interiors and the cave. They did their job: the maps exist.

They are retired because the maps are now **hand-authored in Tiled**, and
re-running any of these would silently overwrite that hand work with a
generated file. The maps are the source of truth; these scripts are their
history.

If you ever need one (to bootstrap a brand-new region, say), copy it out,
point its output at a NEW file, and never at a map that has been touched in
Tiled since.

To add a map today: draw it in Tiled, save it under `Maps/`, and add one line
to `src/AlfarQuest.Client/wwwroot/Maps/manifest.json`. Nothing else.
