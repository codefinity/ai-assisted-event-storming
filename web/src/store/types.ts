import type { ThunkAction, UnknownAction } from '@reduxjs/toolkit';
import type { AppStore, RootState } from './store';

export type { RootState, AppStore };

export type AppDispatch = AppStore['dispatch'];

export type AppThunk<Result = void> = ThunkAction<Result, RootState, unknown, UnknownAction>;
