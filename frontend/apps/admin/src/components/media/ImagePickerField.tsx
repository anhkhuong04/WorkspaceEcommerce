import { useMutation } from "@tanstack/react-query";
import type { ChangeEvent } from "react";
import { useEffect, useId, useRef, useState } from "react";
import { Button, TextInput } from "../ui/AdminUi";
import { adminApi } from "../../services/api/adminApi";
import { getApiErrorMessage } from "../../services/api/errors";

interface ImagePickerFieldProps {
  label: string;
  value: string;
  folder: "products" | "banners" | "blogs" | "general";
  error?: string;
  placeholder?: string;
  onChange: (value: string) => void;
  onUploadingChange?: (uploading: boolean) => void;
}

const MAX_FILE_BYTES = 2 * 1024 * 1024; // 2 MB

export function ImagePickerField({
  label,
  value,
  folder,
  error,
  placeholder = "https://example.test/image.webp",
  onChange,
  onUploadingChange,
}: ImagePickerFieldProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();
  const [uploadError, setUploadError] = useState<string | null>(null);
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    return () => { active.current = false; onUploadingChange?.(false); };
  }, [onUploadingChange]);

  const uploadMutation = useMutation({
    mutationFn: (file: File) => adminApi.uploadMedia(file, folder),
    onMutate: () => onUploadingChange?.(true),
    onSettled: () => onUploadingChange?.(false),
    onSuccess: (result) => {
      if (!active.current) return;
      setUploadError(null);
      onChange(result.url);
    },
    onError: (err) => setUploadError(getApiErrorMessage(err)),
  });

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const [file] = event.target.files ?? [];
    if (!file) {
      return;
    }

    if (file.size > MAX_FILE_BYTES) {
      setUploadError(
        `File is too large (${(file.size / 1024 / 1024).toFixed(1)} MB). Maximum allowed size is 2 MB.`,
      );
      event.target.value = "";
      return;
    }

    setUploadError(null);
    uploadMutation.mutate(file);
    event.target.value = "";
  }

  return (
    <div>
      <label
        htmlFor={inputId}
        className="mb-1.5 block text-sm font-medium text-slate-700"
      >
        {label}
      </label>
      {value ? (
        <div className="mb-3 overflow-hidden rounded-xl border border-slate-200 bg-slate-50">
          <img className="h-40 w-full object-contain" src={value} alt="" />
        </div>
      ) : null}
      <div className="flex flex-col gap-2 sm:flex-row">
        <TextInput
          id={inputId}
          value={value}
          disabled={uploadMutation.isPending}
          aria-invalid={Boolean(error || uploadError)}
          aria-describedby={[
            `${inputId}-help`,
            error ? `${inputId}-validation` : null,
            uploadError ? `${inputId}-upload-error` : null,
          ]
            .filter(Boolean)
            .join(" ")}
          onChange={(event) => onChange(event.target.value)}
          placeholder={placeholder}
        />
        <input
          ref={inputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={handleFileChange}
        />
        <Button
          type="button"
          disabled={uploadMutation.isPending}
          onClick={() => inputRef.current?.click()}
          className="sm:w-36"
        >
          {uploadMutation.isPending ? "Uploading..." : "Choose image"}
        </Button>
      </div>
      <p
        id={`${inputId}-help`}
        className="mt-1 text-xs font-medium text-slate-500"
      >
        JPEG, PNG or WebP · max 2 MB
      </p>
      {error ? (
        <span
          id={`${inputId}-validation`}
          className="mt-1 block text-sm font-semibold text-red-600"
          role="alert"
        >
          {error}
        </span>
      ) : null}
      {uploadError ? (
        <span
          id={`${inputId}-upload-error`}
          className="mt-1 block text-sm font-semibold text-red-600"
          role="alert"
        >
          {uploadError}
        </span>
      ) : null}
    </div>
  );
}
