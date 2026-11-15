# Changelog

## v1.1.0

- Added automatic foundation for underwater veins.
- Added the `General.AutoFoundation` config option (enabled by default).
- Foundation size follows the miner footprint and consumes foundation items/soil; already-founded cells are not charged again.
- Retries foundation for `NeedGround`/`NeedWater` candidates and supports sandbox Infinite Sand.
- Existing land placements are preferred over spending foundation materials.
