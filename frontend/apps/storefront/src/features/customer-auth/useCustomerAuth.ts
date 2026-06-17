import { useContext } from "react";
import { CustomerAuthContext } from "./CustomerAuthContext";

export function useCustomerAuth() {
  const value = useContext(CustomerAuthContext);
  if (!value) {
    throw new Error("useCustomerAuth must be used inside CustomerAuthProvider.");
  }

  return value;
}
