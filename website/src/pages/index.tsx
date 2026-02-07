import type {ReactNode} from 'react';
import {useState} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import AsciinemaPlayer from '@site/src/components/AsciinemaPlayer';

const features = [
  {
    tag: 'Ask & Debug',
    name: 'AI-Driven Debugging',
    desc: 'Describe the bug in plain language — your AI agent launches the app, sets breakpoints, and inspects the state for you.',
  },
  {
    tag: '34 Tools',
    name: 'Complete Toolkit',
    desc: 'Breakpoints, stepping, variables, expressions, memory layout, exception autopsy, code analysis — everything a debugger needs.',
  },
  {
    tag: 'One Command',
    name: 'Zero Installation',
    desc: <>Run instantly with <code>dotnet tool run debug-mcp</code> on .NET 10+. No global install, no config files, ready in seconds.</>,
  },
];

function QuickStartStep({number, title, children}: {number: number; title: string; children: ReactNode}) {
  const [copied, setCopied] = useState(false);
  const text = typeof children === 'string' ? children : null;

  function handleCopy() {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="quickstart-step">
      <div className="quickstart-step-number">{number}</div>
      <div className="quickstart-step-title">{title}</div>
      <div className="quickstart-step-content">
        {typeof children === 'string' ? (
          <pre className="quickstart-code">
            <code>{children}</code>
            <button className="quickstart-copy" onClick={handleCopy} title="Copy to clipboard">
              {copied ? 'Copied!' : 'Copy'}
            </button>
          </pre>
        ) : (
          <div className="quickstart-inline">{children}</div>
        )}
      </div>
    </div>
  );
}

export default function Home(): ReactNode {
  return (
    <Layout
      title="AI-powered .NET debugging"
      description="Let your AI agent set breakpoints, inspect variables, and find bugs in your .NET apps">
      <div className="landing">
        {/* Hero */}
        <section className="hero-section">
          <div className="hero-inner">
            <div className="hero-label">Model Context Protocol Server</div>
            <h1 className="hero-title">
              debug<span className="dot">-</span>mcp<span className="dot">.</span>net
            </h1>
            <p className="hero-tagline">
              Let your AI agent set breakpoints, inspect variables, and
              find bugs — in your .NET apps.
            </p>
            <div className="hero-actions">
              <Link className="btn-primary" to="/docs/getting-started">
                Get Started →
              </Link>
              <Link className="btn-ghost" href="https://github.com/jkolo/debug-mcp">
                GitHub
              </Link>
            </div>
          </div>
        </section>

        {/* Terminal Recording */}
        <section className="terminal-section">
          <AsciinemaPlayer
            src="/casts/setup-mcp.cast"
            rows={24}
            cols={120}
            idleTimeLimit={2}
            speed={1.5}
            fit="width"
          />
        </section>

        {/* Quick Start */}
        <section className="quickstart-section">
          <div className="quickstart-header">
            <div className="features-label">Quick Start</div>
            <h2 className="features-title">Up and running in 3 steps</h2>
          </div>
          <div className="quickstart-steps">
            <QuickStartStep number={1} title="Install">
              <code>dotnet tool install -g debug-mcp</code>
            </QuickStartStep>
            <QuickStartStep number={2} title="Configure">
{`{
  "mcpServers": {
    "dotnet-debugger": {
      "command": "debug-mcp"
    }
  }
}`}
            </QuickStartStep>
            <QuickStartStep number={3} title="Debug">
              <span className="quickstart-prompt">"Launch MyApp.dll and find why GetUser throws null"</span>
            </QuickStartStep>
          </div>
        </section>

        {/* Features */}
        <section className="features-section">
          <div className="features-header">
            <div className="features-label">Capabilities</div>
            <h2 className="features-title">Everything your AI agent needs to debug .NET</h2>
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
