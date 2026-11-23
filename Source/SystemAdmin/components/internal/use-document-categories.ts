"use client";
import { useEffect, useState } from "react";
import { DOCUMENT_TYPES, loadDocumentCategories, type DocumentCategory } from "@/services/internal-api";

/** The document types (Loại văn bản), read once per page load; null while loading or when the list could not be read.
 * Using it also re-renders the component once documentTypeLabel knows the custom names. */
export function useDocumentCategories(): DocumentCategory[] | null {
  const [list, setList] = useState<DocumentCategory[] | null>(null);
  useEffect(() => {
    let active = true;
    loadDocumentCategories().then(value => { if (active) setList(value); }).catch(() => undefined);
    return () => { active = false; };
  }, []);
  return list;
}

/** Options for a type select: the active types plus the one already chosen (a template may keep a hidden type);
 * all types with all=true (a filter); the built-in five until the list is read. */
export function typeOptions(list: DocumentCategory[] | null, current?: number | null, all = false): { id: number; name: string }[] {
  if (!list) return DOCUMENT_TYPES.map((name, index) => ({ id: index + 1, name }));
  return list.filter(c => all || c.active || c.id === current).map(c => ({ id: c.id, name: c.name + (c.active ? "" : " (đã ẩn)") }));
}
