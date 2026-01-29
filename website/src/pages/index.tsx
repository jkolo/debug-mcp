import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';

const features = [
  {
    tag: 'ICorDebug',
    name: 'Direct ICorDebug Access',
    desc: 'Interfaces directly with the .NET runtime using ICorDebug APIs — the same approach used by JetBrains Rider. No DAP overhead.',
  },
  {
    tag: 'Tools',
    name: 'Full Debug Toolkit',
    desc: 'Launch, attach, breakpoints, stepping, variable inspection, expression evaluation, stack traces, memory layout analysis.',
  },
  {
    tag: '.NET 10',
    name: 'Zero Installation',
    desc: <>Run instantly with <code>dnx debug-mcp</code> on .NET 10+. No global install needed.</>,
  },
];

export default function Home(): ReactNode {
  return (
    <Layout
      title="MCP server for .NET debugging"
      description="Enable AI agents to debug .NET applications interactively via ICorDebug APIs">
      <div className="landing">
        {/* Hero */}
        <section className="hero-section">
          <div className="hero-inner">
            <div className="hero-label">Model Context Protocol Server</div>
            <h1 className="hero-title">
              debug<span className="dot">-</span>mcp<span className="dot">.</span>net
            </h1>
            <p className="hero-tagline">
              MCP server for .NET debugging — enable AI agents to debug
              .NET applications interactively.
            </p>
            <div className="hero-actions">
              <Link className="btn-primary" to="/docs/architecture">
                Get Started →
              </Link>
              <Link className="btn-ghost" href="https://github.com/jkolo/netinspect-mcp">
                GitHub
              </Link>
            </div>
          </div>
        </section>

        {/* Terminal */}
        <section className="terminal-section">
          <div className="terminal">
            <div className="terminal-bar">
              <div className="terminal-dot" />
              <div className="terminal-dot" />
              <div className="terminal-dot" />
              <span>terminal</span>
              <div style={{width: 30}} />
            </div>
            <div className="terminal-body">
              <div className="terminal-comment"># no install needed</div>
              <div className="terminal-line">
                <span className="terminal-prompt">$</span>
                <span className="terminal-cmd">dnx debug-mcp</span>
              </div>
            </div>
          </div>
        </section>

        {/* Features */}
        <section className="features-section">
          <div className="features-header">
            <div className="features-label">Capabilities</div>
            <h2 className="features-title">Native .NET debugging for AI agents</h2>
          </div>
          <div className="features-grid">
            {features.map((f) => (
              <div className="feature-card" key={f.name}>
                <div className="feature-icon">{f.tag}</div>
                <div className="feature-name">{f.name}</div>
                <p className="feature-desc">{f.desc}</p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </Layout>
  );
}
