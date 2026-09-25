import { api } from '@/shared/api/baseApi';
import { sessionEnded, sessionStarted, type Account, type SessionResponse } from './sessionSlice';

export interface SignUpRequest {
  email: string;
  displayName: string;
  password: string;
}

export interface SignInRequest {
  email: string;
  password: string;
}

export const identityApi = api.injectEndpoints({
  endpoints: (build) => ({
    signUp: build.mutation<SessionResponse, SignUpRequest>({
      query: (body) => ({ url: '/auth/sign-up', method: 'POST', body, anonymous: true }),
      async onQueryStarted(_, { dispatch, queryFulfilled }) {
        const { data } = await queryFulfilled;
        dispatch(sessionStarted(data));
      },
    }),
    signIn: build.mutation<SessionResponse, SignInRequest>({
      query: (body) => ({ url: '/auth/sign-in', method: 'POST', body, anonymous: true }),
      async onQueryStarted(_, { dispatch, queryFulfilled }) {
        const { data } = await queryFulfilled;
        dispatch(api.util.resetApiState());
        dispatch(sessionStarted(data));
      },
    }),
    signOut: build.mutation<void, void>({
      query: () => ({ url: '/auth/sign-out', method: 'POST', anonymous: true }),
      async onQueryStarted(_, { dispatch, queryFulfilled }) {
        try {
          await queryFulfilled;
        } finally {
          dispatch(sessionEnded());
          dispatch(api.util.resetApiState());
        }
      },
    }),
    getMe: build.query<Account, void>({
      query: () => '/me',
      providesTags: ['Me'],
    }),
  }),
});

export const { useSignUpMutation, useSignInMutation, useSignOutMutation } = identityApi;
