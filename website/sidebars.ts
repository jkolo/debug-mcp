import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'getting-started',
    {
      type: 'category',
      label: 'Tools',
      link: {
        type: 'doc',
        id: 'tools/index',
      },
      items: [
        'tools/session',
        'tools/breakpoints',
        'tools/execution',
        'tools/inspection',
        'tools/memory',
        'tools/modules',
        'tools/code-analysis',
        'tools/process-io',
      ],
    },
    'resources',
    {
      type: 'category',
      label: 'Workflows',
      items: [
        'workflows/debug-with-breakpoints',
        'workflows/debug-an-exception',
        'workflows/inspect-memory-layout',
        'workflows/explore-application-structure',
        'workflows/analyze-codebase',
      ],
    },
    {
      type: 'category',
      label: 'Internals',
      items: [
        'architecture',
        'debugger',
        'development',
      ],
    },
  ],
};

export default sidebars;
