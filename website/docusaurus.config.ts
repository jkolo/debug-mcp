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
          editUrl: 'https://github.com/jkolo/netinspect-mcp/tree/main/website/',
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
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'debug-mcp.net',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {
          href: 'https://github.com/jkolo/netinspect-mcp',
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
            {label: 'Architecture', to: '/docs/architecture'},
            {label: 'MCP Tools Reference', to: '/docs/mcp-tools'},
            {label: 'Development Guide', to: '/docs/development'},
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/jkolo/netinspect-mcp',
            },
            {
              label: 'NuGet',
              href: 'https://www.nuget.org/packages/NetInspect.Mcp',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Jerzy Kołosowski. AGPL-3.0 License.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
