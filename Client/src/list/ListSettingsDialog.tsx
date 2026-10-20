import { useRef, useState, type RefObject, type SubmitEvent } from "react";
import type { ItemGroupSettings, ItemGroupSettingsInput } from "../api/types";
import { Dialog, DialogActions } from "../components/Dialog";
import {
  DURATION_UNITS,
  MAX_SETTING_MINUTES,
  MIN_AUTO_DELETE_MINUTES,
  UNIT_MINUTES,
  toDuration,
  type DurationUnit,
} from "./duration";
import styles from "./ListPage.module.css";

interface ListSettingsDialogProps {
  open: boolean;
  settings: ItemGroupSettings;
  onSave: (input: ItemGroupSettingsInput) => Promise<void>;
  onClose: () => void;
}

export function ListSettingsDialog({ open, settings, onSave, onClose }: ListSettingsDialogProps) {
  return (
    <Dialog
      open={open}
      title="List settings"
      description="These settings apply to everyone in this list."
      onClose={onClose}
    >
      {open && <SettingsForm settings={settings} onSave={onSave} onClose={onClose} />}
    </Dialog>
  );
}

interface SettingState {
  enabled: boolean;
  amount: string;
  unit: DurationUnit;
}

/** Duration shown when a setting that is off gets turned on. */
const DEFAULT_HIDE_MINUTES = UNIT_MINUTES.days;
const DEFAULT_AUTO_DELETE_MINUTES = 7 * UNIT_MINUTES.days;

function initialSetting(minutes: number | null, fallback: number): SettingState {
  const { amount, unit } = toDuration(minutes ?? fallback);
  return { enabled: minutes !== null, amount: String(amount), unit };
}

function validate(
  setting: SettingState,
  min: number,
  tooShort: string,
): { minutes: number | null; error?: never } | { error: string } {
  if (!setting.enabled) return { minutes: null };
  const amount = setting.amount.trim();
  if (!/^\d+$/.test(amount)) return { error: "Enter a whole number." };
  const minutes = Number(amount) * UNIT_MINUTES[setting.unit];
  if (minutes < min) return { error: tooShort };
  if (minutes > MAX_SETTING_MINUTES) return { error: "Choose 365 days or less." };
  return { minutes };
}

function SettingsForm({ settings, onSave, onClose }: Omit<ListSettingsDialogProps, "open">) {
  const [hide, setHide] = useState(() =>
    initialSetting(settings.hideCompletedAfterMinutes, DEFAULT_HIDE_MINUTES),
  );
  const [autoDelete, setAutoDelete] = useState(() =>
    initialSetting(settings.autoDeleteAfterMinutes, DEFAULT_AUTO_DELETE_MINUTES),
  );
  const [errors, setErrors] = useState<{
    hide?: string | undefined;
    autoDelete?: string | undefined;
  }>({});
  const [pending, setPending] = useState(false);
  const hideAmountRef = useRef<HTMLInputElement>(null);
  const autoDeleteAmountRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    const hideResult = validate(hide, 0, "");
    const autoDeleteResult = validate(
      autoDelete,
      MIN_AUTO_DELETE_MINUTES,
      "Choose at least 1 day, so there's time to change your mind.",
    );
    if (hideResult.error !== undefined || autoDeleteResult.error !== undefined) {
      setErrors({ hide: hideResult.error, autoDelete: autoDeleteResult.error });
      (hideResult.error !== undefined ? hideAmountRef : autoDeleteAmountRef).current?.focus();
      return;
    }

    setErrors({});
    setPending(true);
    try {
      await onSave({
        hideCompletedAfterMinutes: hideResult.minutes,
        autoDeleteAfterMinutes: autoDeleteResult.minutes,
      });
    } catch {
      setPending(false);
    }
  }

  return (
    <form
      className={styles.dialogForm}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <SettingFields
        id="hide-done"
        label="Hide done items"
        hint="Done items disappear from “All” this long after they're checked. You can still find them under “Done”."
        durationLabel="Hide after"
        setting={hide}
        error={errors.hide}
        amountRef={hideAmountRef}
        onChange={setHide}
        initialFocus
      />
      <SettingFields
        id="auto-delete"
        label="Delete the list when every item is done"
        hint="The list and its items are deleted for every member this long after the last item is checked. Unchecking or adding an item stops the countdown."
        durationLabel="Delete after"
        setting={autoDelete}
        error={errors.autoDelete}
        amountRef={autoDeleteAmountRef}
        onChange={setAutoDelete}
      />
      <DialogActions>
        <button type="button" className="button" onClick={onClose}>
          Cancel
        </button>
        <button type="submit" className="button button-primary" aria-disabled={pending}>
          {pending ? "Saving…" : "Save"}
        </button>
      </DialogActions>
    </form>
  );
}

interface SettingFieldsProps {
  id: string;
  label: string;
  hint: string;
  durationLabel: string;
  setting: SettingState;
  error: string | undefined;
  amountRef: RefObject<HTMLInputElement | null>;
  onChange: (setting: SettingState) => void;
  initialFocus?: boolean;
}

/** A checkbox that turns the setting on, revealing how long to wait. */
function SettingFields({
  id,
  label,
  hint,
  durationLabel,
  setting,
  error,
  amountRef,
  onChange,
  initialFocus = false,
}: SettingFieldsProps) {
  const errorId = `${id}-error`;

  return (
    <div className={styles.setting}>
      <div className={styles.settingToggle}>
        <input
          type="checkbox"
          id={`${id}-enabled`}
          className={styles.settingCheckbox}
          checked={setting.enabled}
          onChange={(event) => {
            onChange({ ...setting, enabled: event.target.checked });
          }}
          aria-describedby={`${id}-hint`}
          data-autofocus={initialFocus || undefined}
        />
        <div className={styles.settingText}>
          <label htmlFor={`${id}-enabled`} className="label">
            {label}
          </label>
          <p id={`${id}-hint`} className="hint">
            {hint}
          </p>
        </div>
      </div>

      {setting.enabled && (
        <fieldset className={styles.duration}>
          <legend className="label">{durationLabel}</legend>
          <div className={styles.durationFields}>
            <div className={`field ${styles.durationAmount}`}>
              <label htmlFor={`${id}-amount`}>Amount</label>
              <input
                ref={amountRef}
                id={`${id}-amount`}
                className="input"
                inputMode="numeric"
                autoComplete="off"
                value={setting.amount}
                onChange={(event) => {
                  onChange({ ...setting, amount: event.target.value });
                }}
                aria-invalid={error ? true : undefined}
                aria-describedby={error ? errorId : undefined}
              />
            </div>
            <div className={`field ${styles.durationUnit}`}>
              <label htmlFor={`${id}-unit`}>Unit</label>
              <select
                id={`${id}-unit`}
                className="input"
                value={setting.unit}
                onChange={(event) => {
                  onChange({ ...setting, unit: event.target.value as DurationUnit });
                }}
              >
                {DURATION_UNITS.map((unit) => (
                  <option key={unit} value={unit}>
                    {unit}
                  </option>
                ))}
              </select>
            </div>
          </div>
          {error && (
            <p className="field-error" id={errorId}>
              {error}
            </p>
          )}
        </fieldset>
      )}
    </div>
  );
}
