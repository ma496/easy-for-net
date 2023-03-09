import {NotificationProps} from "@mantine/notifications/lib/types";
import {showNotification} from "@mantine/notifications";

export function successNotification(props: NotificationProps) {
  showNotification({
    ...props,
    color: props.color ? props.color : 'green',
  });
}

export function errorNotification(props: NotificationProps) {
  showNotification({
    ...props,
    color: props.color ? props.color : 'red',
  });
}

export function infoNotification(props: NotificationProps) {
  showNotification({
    ...props,
  });
}
