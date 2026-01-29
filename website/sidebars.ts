import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'getting-started',
    {
      type: 'category',
      label: 'Tools',
      items: [
        'tools/session',
        'tools/breakpoints',
        'tools/execution',
        'tools/inspection',
        'tools/memory',
        'tools/modules',
      ],
    },
    {
      type: 'category',
      label: 'Workflows',
      items: [
        'workflows/debug-a-crash',
        'workflows/inspect-memory-layout',
        'workflows/profile-module-loading',
      ],
    },
    'architecture',
    'debugger',
    'development',
  ],
};

export default sidebars;
