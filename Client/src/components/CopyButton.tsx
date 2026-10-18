import { useEffect, useState } from "react";
import { Icon } from "./Icon";
import { useToast } from "./Toasts";

interface CopyButtonProps {
  value: string;
  label: string;
  className?: string;
}

export function CopyButton({ value, label, className = "button button-small" }: CopyButtonProps) {
  const { announce, notify } = useToast();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => {
      setCopied(false);
    }, 2000);
    return () => {
      clearTimeout(timer);
    };
  }, [copied]);

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      announce("Copied to clipboard.");
    } catch {
      notify("Couldn't copy automatically. Select the text and copy it instead.", "error");
    }
  }

  return (
    <button
      type="button"
      className={className}
      onClick={() => {
        void copy();
      }}
    >
      <Icon name={copied ? "check" : "copy"} size={16} />
      {copied ? "Copied" : label}
    </button>
  );
}
