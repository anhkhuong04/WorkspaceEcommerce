import { Link, isRouteErrorResponse, useRouteError } from "react-router-dom";
import { AlertTriangle, ArrowLeft } from "lucide-react";
import { Button } from "../components/ui/AdminUi";

export function AdminErrorPage({ notFound = false }: { notFound?: boolean }) {
  const error = useRouteError();
  const missing =
    notFound || (isRouteErrorResponse(error) && error.status === 404);
  return (
    <section className="admin-panel mx-auto my-8 max-w-xl p-8 text-center">
      <AlertTriangle
        size={32}
        className="mx-auto text-[var(--brand)]"
        aria-hidden="true"
      />
      <h1 className="mt-4 text-2xl font-semibold">
        {missing ? "Page not found" : "This page could not be loaded"}
      </h1>
      <p className="my-4 text-sm text-[var(--muted)]">
        {missing
          ? "Check the address or return to your dashboard."
          : "Try loading the page again. Your saved data is available when the connection recovers."}
      </p>
      <div className="flex flex-wrap justify-center gap-3">
        <Link to="/" className="admin-button admin-button-secondary">
          <ArrowLeft size={16} aria-hidden="true" />
          Dashboard
        </Link>
        {!missing && (
          <Button variant="primary" onClick={() => window.location.reload()}>
            Try again
          </Button>
        )}
      </div>
    </section>
  );
}
