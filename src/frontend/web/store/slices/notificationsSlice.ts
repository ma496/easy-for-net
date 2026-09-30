import { createSlice, PayloadAction } from '@reduxjs/toolkit'

interface NotificationsState {
  unreadCount: number;
}

const initialState: NotificationsState = {
  unreadCount: 0,
};

/**
 * Notifications slice holding the current unread notification count, which the header bell renders as
 * its badge. `useNotificationHub` keeps it live: `notificationReceived` counts one more for each
 * notification the hub pushes, and `setUnreadCount` replaces the count with an authoritative one - the
 * hub's `unreadCountChanged` message, or a fetch of the unread-count endpoint after every connect and
 * while the hub is disconnected.
 */
export const notificationsSlice = createSlice({
  name: 'notifications',
  initialState,
  reducers: {
    setUnreadCount(state, action: PayloadAction<number>) {
      state.unreadCount = action.payload;
    },
    // A notification just raised is unread for everyone it reaches, so it is one more unread without
    // asking the API; the next authoritative count corrects any drift.
    notificationReceived(state) {
      state.unreadCount += 1;
    },
  },
});

export const { setUnreadCount, notificationReceived } = notificationsSlice.actions;
