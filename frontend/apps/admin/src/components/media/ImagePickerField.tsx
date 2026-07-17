import { useMutation } from "@tanstack/react-query";
import type { ChangeEvent } from "react";
import { useRef, useState } from "react";
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
}

export function ImagePickerField({
  label,
  value,
  folder,
  error,
  placeholder = "https://example.test/image.webp",
  onChange
}: ImagePickerFieldProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const uploadMutation = useMutation({
    mutationFn: (file: File) => adminApi.uploadMedia(file, folder),
    onSuccess: (result) => {
      setUploadError(null);
      onChange(result.url);
    },
    onError: (err) => setUploadError(getApiErrorMessage(err))
  });

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const [file] = event.target.files ?? [];
    if (!file) {
      return;
    }

    uploadMutation.mutate(file);
    event.target.value = "";
  }

  return (
    <div>
      <span className="mb-1.5 block text-sm font-bold text-slate-700">{label}</span>
      {value ? (
        <div className="mb-3 overflow-hidden rounded-xl border border-slate-200 bg-slate-50">
          <img className="h-40 w-full object-cover" src={value} alt="" />
        </div>
      ) : null}
      <div className="flex flex-col gap-2 sm:flex-row">
        <TextInput value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />
        <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp,image/gif" className="hidden" onChange={handleFileChange} />
        <Button type="button" disabled={uploadMutation.isPending} onClick={() => inputRef.current?.click()} className="sm:w-36">
          {uploadMutation.isPending ? "Uploading..." : "Choose image"}
        </Button>
      </div>
      {error ? <span className="mt-1 block text-sm font-semibold text-red-600" role="alert">{error}</span> : null}
      {uploadError ? <span className="mt-1 block text-sm font-semibold text-red-600" role="alert">{uploadError}</span> : null}
    </div>
  );
}
