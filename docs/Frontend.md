# Updating NuGet Gallery's frontend

## Styling

We use [Bootstrap 3](https://getbootstrap.com/docs/3.3/) as our base CSS framework.
This provides a grid layout, CSS normalization, and some common styles.

Changes to our styles should be made to [LESS](https://lesscss.org/) files and not to a CSS file directly.
LESS is a syntax that itself is not usable by a browser but is compiled to CSS. It provides niceties that CSS
does not offer such as rule nesting, mixins, variables, and arithmetic (calculated values).

Each page may have its own set of specific styles. These page-specific styles are in a `page-*.less` file
in the `src\Bootstrap\less\theme\` directory.

### Prerequisites

To compile LESS files:

1. Install node: https://nodejs.org/en/download/
1. Install Grunt: `npm install -g grunt`
1. Navigate to `.\src\Bootstrap`
1. Install NPM dependencies: `npm install`

### Updating styling

1. Update one or more `.less` files in the `src\Bootstrap\less` directory
1. Navigate to `.\src\Bootstrap`
1. Run `grunt`

### Adding a new page

1. Create a new `page-X.less` file in the `src\Bootstrap\less\theme\` directory
1. Add the new page in `src\Bootstrap\less\theme\all.less`

## JavaScript

We use [jQuery](https://jquery.com/) and [Knockout.js](https://knockoutjs.com/).

Common JavaScript should be added to `src\NuGetGallery\Scripts\gallery\common.js`.

Each page may have its own custom logic. These page-specific scripts are in a `page-*.js` file
in the `src\NuGetGallery\Scripts\gallery\` directory.
