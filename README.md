# Open RCT3
An open source re-implementation of [RollerCoaster Tycoon 3](https://www.frontier.co.uk/our-games/our-gameography/#rollercoastertycoon3).

## Roadmap

Our rough plan is:

### [Phase 1](https://github.com/open-rct3/open-rct3/milestone/1): Design, Prototyping, and Engine Scaffolding

- [x] Create a website and community
- [ ] Render a flat, empty park
- [ ] Render terrain
- [ ] Render scenery
- [ ] Render attractions, including rides and roller coasters
- [ ] [Iterate](https://en.wikipedia.org/wiki/Iterative_and_incremental_development)!

### [Phase 2](https://github.com/open-rct3/open-rct3/milestone/2): Gameplay

- Base Game, then
- Wild!
- Soaked!

### [Phase 3](https://github.com/open-rct3/open-rct3/milestone/3): Plugins

- UI and Quality of Life Enhancements:
  - Freeform, off-grid scenery placement
  - GUI Enhancements
    - Resizable GUI Windows, including:
    - Query search for Structures and Scenery
    - Filterable lists for Parks, Attractions, Structures, and Scenery
  - etc.
- Procedural Tracks

### [Future](https://github.com/open-rct3/open-rct3/milestone/4): Open Graphics

Players won't require an existing copy of RCT3.

----

See the full [Roadmap](https://github.com/open-rct3/open-rct3/wiki/Roadmap) on our wiki for more details.

## Development

1. Install [D](https://dlang.org/install), [Deno](https://docs.deno.com/runtime/#install-deno), and [`blogc`]().

    `blogc` is also available as a Brew bottle:
    ```shell
    brew install blogc
    ```
2. Build OpenRCT3:
    ```shell
    dub build
    ```
3. Run the development server:
    ```shell
   make debug
    ```

## Disclaimer of Warranty

This is a _volunteer-driven_ project. **Please** temper your expectations accordingly.
