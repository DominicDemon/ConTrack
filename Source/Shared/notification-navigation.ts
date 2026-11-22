const refreshEvent = "contrack:notification-refresh";

export function openNotificationTarget(destination: string, navigate: (href: string) => void) {
  const current = new URL(window.location.href);
  const target = new URL(destination, current);
  current.searchParams.sort();
  target.searchParams.sort();
  const pathname = (url: URL) => url.pathname.replace(/\/$/, "") || "/";
  if (current.origin === target.origin && pathname(current) === pathname(target) && current.search === target.search) {
    window.dispatchEvent(new Event(refreshEvent));
  } else {
    navigate(destination);
  }
}

export function onNotificationRefresh(refresh: () => void) {
  window.addEventListener(refreshEvent, refresh);
  return () => window.removeEventListener(refreshEvent, refresh);
}
