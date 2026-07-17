import { Button, SelectInput, TextInput } from "../../../components/ui/AdminUi";

type CouponFiltersProps = {
  searchFilter: string | undefined;
  activeFilter: boolean | undefined;
  effectiveAtFilter: string;
  onUpdateFilters: (values: { search?: string; isActive?: boolean; effectiveAt?: string }) => void;
};

export function CouponFilters({
  searchFilter,
  activeFilter,
  effectiveAtFilter,
  onUpdateFilters
}: CouponFiltersProps) {
  return (
    <div className="mb-4 grid gap-3 lg:grid-cols-[minmax(220px,1fr)_180px_220px_auto]">
      <TextInput
        key={searchFilter ?? ""}
        defaultValue={searchFilter ?? ""}
        placeholder="Search code or name"
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            onUpdateFilters({ search: event.currentTarget.value.trim() || undefined, isActive: activeFilter, effectiveAt: effectiveAtFilter });
          }
        }}
      />
      <SelectInput
        value={activeFilter === undefined ? "" : String(activeFilter)}
        onChange={(event) => onUpdateFilters({
          search: searchFilter,
          isActive: event.target.value === "" ? undefined : event.target.value === "true",
          effectiveAt: effectiveAtFilter
        })}
      >
        <option value="">All statuses</option>
        <option value="true">Active only</option>
        <option value="false">Inactive only</option>
      </SelectInput>
      <TextInput
        key={effectiveAtFilter}
        type="datetime-local"
        defaultValue={effectiveAtFilter}
        onChange={(event) => onUpdateFilters({ search: searchFilter, isActive: activeFilter, effectiveAt: event.currentTarget.value })}
      />
      <Button type="button" onClick={() => onUpdateFilters({ search: undefined, isActive: undefined, effectiveAt: undefined })}>Clear filters</Button>
    </div>
  );
}
