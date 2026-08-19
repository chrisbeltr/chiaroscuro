import { useCallback, useEffect, useRef, useState } from 'react';
import { useCalculatorStore } from './state/useCalculatorStore';
import { useGeolocation } from './api/useGeolocation';
import { useIllumination } from './api/useIllumination';
import { LocationTimePanel } from './components/panels/LocationTimePanel';
import { RoomPanel } from './components/panels/RoomPanel';
import { WindowPanel } from './components/panels/WindowPanel';
import { TargetPointPanel } from './components/panels/TargetPointPanel';
import { ResultPanel } from './components/results/ResultPanel';
import { AlignmentMatchList } from './components/results/AlignmentMatchList';
import { RoomViewportCanvas } from './components/viewport/RoomViewportCanvas';
import './App.css';

const MIN_SIDEBAR_WIDTH = 280;
const MAX_SIDEBAR_WIDTH = 640;
const DEFAULT_SIDEBAR_WIDTH = 350;

export default function App() {
  const { resolve } = useGeolocation();
  const applyGeolocation = useCalculatorStore((state) => state.applyGeolocation);
  const seedTargetIfUnset = useCalculatorStore((state) => state.seedTargetIfUnset);
  const illumination = useIllumination();

  const [sidebarWidth, setSidebarWidth] = useState(DEFAULT_SIDEBAR_WIDTH);
  const isResizing = useRef(false);

  const handleResizeStart = useCallback((event: React.PointerEvent) => {
    event.preventDefault();
    isResizing.current = true;
  }, []);

  useEffect(() => {
    function handlePointerMove(event: PointerEvent) {
      if (!isResizing.current) return;
      setSidebarWidth(Math.min(MAX_SIDEBAR_WIDTH, Math.max(MIN_SIDEBAR_WIDTH, event.clientX)));
    }
    function handlePointerUp() {
      isResizing.current = false;
    }
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, []);

  // Mirrors MainViewModel's constructor: resolve the current location once at startup.
  useEffect(() => {
    resolve().then((location) => {
      if (location) {
        applyGeolocation(location);
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Mirrors MainViewModel's constructor: seed the inverse-solver target from wherever the sun
  // first lands (or the floor center), once, the first time an illumination result comes back.
  useEffect(() => {
    if (illumination.data) {
      const point = illumination.data.illumination?.centerPoint ?? { x: 0, y: 0, z: 0 };
      seedTargetIfUnset(point);
    }
  }, [illumination.data, seedTargetIfUnset]);

  return (
    <div className="app-layout">
      <aside className="app-sidebar-container" style={{ width: sidebarWidth, maxHeight: "100dvh" }}>
        <div className="app-sidebar" style={{ width: "100%", height: "100%" }}>
          <h1 className="app-title">Chiaroscuro</h1>
          <LocationTimePanel />
          <RoomPanel />
          <WindowPanel />
          <TargetPointPanel />
        </div>
      </aside>
      <div
        className="app-resizer"
        role="separator"
        aria-orientation="vertical"
        aria-label="Resize sidebar"
        onPointerDown={handleResizeStart}
      />
      <main className="app-main">
        <RoomViewportCanvas />
        <ResultPanel illumination={illumination} />
        <AlignmentMatchList />
      </main>
    </div>
  );
}
