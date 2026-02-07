import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'debug-mcp.net',
  tagline: 'MCP server for .NET debugging — enable AI agents to debug .NET applications',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  url: 'https://debug-mcp.net',
  baseUrl: '/',

  organizationName: 'jkolo',
  projectName: 'debug-mcp.net',
  trailingSlash: false,

  onBrokenLinks: 'throw',

  markdown: {
    mermaid: true,
  },

  themes: ['@docusaurus/theme-mermaid'],

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          editUrl: 'https://github.com/jkolo/debug-mcp/tree/main/website/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    colorMode: {
      defaultMode: 'dark',
      respectPrefersColorScheme: true,
      disableSwitch: false,
    },
    image: 'img/social-card.jpg',
    navbar: {
      title: 'debug-mcp.net',
      logo: {
        alt: 'debug-mcp logo',
        src: 'img/logo.png',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {
          href: 'https://github.com/jkolo/debug-mcp',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Getting Started', to: '/docs/getting-started'},
            {label: 'Tools Reference', to: '/docs/tools/session'},
            {label: 'Architecture', to: '/docs/architecture'},
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/jkolo/debug-mcp',
            },
            {
              label: 'NuGet',
              href: 'https://www.nuget.org/packages/debug-mcp',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Jerzy Kołosowski. AGPL-3.0 License.`,
    },
    mermaid: {
      theme: {light: 'neutral', dark: 'dark'},
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
