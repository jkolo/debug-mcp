import React, {useEffect, useRef} from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';

interface AsciinemaPlayerProps {
  src: string;
  rows?: number;
  cols?: number;
  idleTimeLimit?: number;
  speed?: number;
  autoPlay?: boolean;
  theme?: string;
  fit?: 'width' | 'height' | 'both' | 'none';
  poster?: string;
}

function Player(props: AsciinemaPlayerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const playerRef = useRef<any>(null);

  useEffect(() => {
    let mounted = true;

    import('asciinema-player').then((module) => {
      // @ts-expect-error -- CSS side-effect import
      import('asciinema-player/dist/bundle/asciinema-player.css');

      if (!mounted || !containerRef.current) return;

      playerRef.current = module.create(props.src, containerRef.current, {
        rows: props.rows,
        cols: props.cols,
        idleTimeLimit: props.idleTimeLimit,
        speed: props.speed,
        autoPlay: props.autoPlay ?? false,
        theme: props.theme,
        fit: props.fit ?? 'width',
        poster: props.poster ?? 'npt:0:01',
      });
    });

    return () => {
      mounted = false;
      playerRef.current?.dispose();
    };
  }, [props.src]);

  return <div ref={containerRef} />;
}

export default function AsciinemaPlayer(props: AsciinemaPlayerProps) {
  return (
    <BrowserOnly fallback={<div>Loading terminal recording...</div>}>
      {() => <Player {...props} />}
    </BrowserOnly>
  );
}
