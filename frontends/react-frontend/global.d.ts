type Messages = typeof import("./messages/en.json");
type UrMessages = typeof import("./messages/ur.json");

declare interface IntlMessages extends Messages, UrMessages {}
