'use client';

import { useEffect, useState, type ReactNode } from 'react';
import { Provider } from 'react-redux';
import { makeStore } from './store';

/** One store per browser tab, made on first render. */
export function StoreProvider({ children }: { children: ReactNode }) {
  const [store] = useState(() => makeStore());

  // Development builds only: lets you inspect state from the browser console.
  useEffect(() => {
    if (process.env.NODE_ENV === 'development') {
      (window as unknown as { __eventStormingStore?: typeof store }).__eventStormingStore = store;
    }
  }, [store]);

  return <Provider store={store}>{children}</Provider>;
}
