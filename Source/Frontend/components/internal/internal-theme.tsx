import type { ComponentProps } from "react";
import styles from "./internal-theme.module.css";

/** Scoped light palette for the internal workspace; legacy screens keep their theme. */
export function InternalTheme({ className = "", ...props }: ComponentProps<"div">) {
  return <div {...props} className={`${styles.theme} ${className}`} />;
}

export { styles as internalStyles };
