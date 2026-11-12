import { RotateCcw, Search } from "lucide-react";
import {
  Button,
  Field,
  SelectInput,
  TextInput,
} from "../../../components/ui/AdminUi";
type Props = {
  searchFilter: string | undefined;
  activeFilter: boolean | undefined;
  effectiveAtFilter: string;
  onUpdateFilters: (values: {
    search?: string;
    isActive?: boolean;
    effectiveAt?: string;
  }) => void;
};
export function CouponFilters({
  searchFilter,
  activeFilter,
  effectiveAtFilter,
  onUpdateFilters,
}: Props) {
  return (
    <form
      className="mb-5 grid items-end gap-3 xl:grid-cols-[minmax(240px,1fr)_150px_220px_auto]"
      onSubmit={(e) => {
        e.preventDefault();
        const search = String(
          new FormData(e.currentTarget).get("search") ?? "",
        ).trim();
        onUpdateFilters({
          search: search || undefined,
          isActive: activeFilter,
          effectiveAt: effectiveAtFilter,
        });
      }}
    >
      <div>
        <label
          htmlFor="coupon-search"
          className="mb-1.5 block text-sm font-medium text-[var(--muted-strong)]"
        >
          Search coupons
        </label>
        <div className="flex gap-2">
          <TextInput
            id="coupon-search"
            name="search"
            type="search"
            key={searchFilter ?? ""}
            defaultValue={searchFilter ?? ""}
            placeholder="Code or name"
          />
          <Button type="submit">
            <Search size={16} aria-hidden="true" />
            <span className="sr-only">Search</span>
          </Button>
        </div>
      </div>
      <Field label="Status">
        <SelectInput
          value={activeFilter === undefined ? "" : String(activeFilter)}
          onChange={(e) =>
            onUpdateFilters({
              search: searchFilter,
              isActive:
                e.target.value === "" ? undefined : e.target.value === "true",
              effectiveAt: effectiveAtFilter,
            })
          }
        >
          <option value="">All statuses</option>
          <option value="true">Active only</option>
          <option value="false">Inactive only</option>
        </SelectInput>
      </Field>
      <Field label="Effective at">
        <TextInput
          type="datetime-local"
          key={effectiveAtFilter}
          defaultValue={effectiveAtFilter}
          onChange={(e) =>
            onUpdateFilters({
              search: searchFilter,
              isActive: activeFilter,
              effectiveAt: e.currentTarget.value,
            })
          }
        />
      </Field>
      <Button
        variant="ghost"
        onClick={(e) => {
          e.currentTarget.form?.reset();
          onUpdateFilters({
            search: undefined,
            isActive: undefined,
            effectiveAt: undefined,
          });
        }}
      >
        <RotateCcw size={16} aria-hidden="true" />
        Reset
      </Button>
    </form>
  );
}
