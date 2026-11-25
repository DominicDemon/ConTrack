"use client";
import WordDocumentLayout, { type DocumentBlock } from "./word-document-layout";
import { createPortal } from "react-dom";
import WordClauseForm, { wordCards, wordCustomerValues, type Card } from "./word-clause-form";
import WordPdfViewer from "./word-pdf-viewer";
import WordTableEditor, { createTable, type AddedTable } from "./word-table-editor";
import WordContentEditor, { type AddedContent } from "./word-content-editor";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import styles from "./template-workspace.module.css";
import { internalApi, loadDocumentCategories, type DocumentCategory, type DraftContent, type TemplateDefinition } from "@/services/internal-api";
import { typeOptions, useDocumentCategories } from "./use-document-categories";

type Block = DocumentBlock;
type Extraction = { name: string; documentBase64: string; blocks: Block[]; sourceToken: string };
export function wordBlob(base64: string, type: string) {
  return new Blob([Uint8Array.from(atob(base64), c => c.charCodeAt(0))], { type });
}
/**
 * parent: the new template is a child (appendix, handover record…) of this contract template.
 * replace: new Word content for this existing template; drafts already made keep their own copy.
 * editTemplate: opens this template's own Word content in the editor (no new file), saved in place.
 * trigger: the page's own button; without it a default "Tạo mẫu văn bản" button opens the editor.
 */
export default function TemplateUpload({ onImported, disabled, draft, locate, inline, parent, replace, trigger, editTemplate, onClose, startOpen }: { inline?: { readOnly?:boolean; templateId:string; content:DraftContent; saved?:DraftContent; previewHost:HTMLElement|null; changed:() => void; receipt: {current:string|null|undefined} }; locate?: {location:string|null;request:number}|null; onImported: (template: TemplateDefinition) => void; disabled?: boolean; draft?: {id:string};
  parent?: { id: string; name: string }; replace?: { id: string; name: string; documentType: number }; trigger?: (open: () => void) => ReactNode;
  editTemplate?: Pick<TemplateDefinition, "id" | "name">; onClose?: () => void; startOpen?: boolean }) {
  const categories = useDocumentCategories();
  const [updatedCategories, setUpdatedCategories] = useState<DocumentCategory[] | null>(null);
  const [otherCategory, setOtherCategory] = useState(false);
  const [categoryName, setCategoryName] = useState("");
  async function chooseCategory() {
    const name = categoryName.trim();
    if (!name || busy) return;
    setBusy(true); setError("");
    const find = (list: DocumentCategory[]) => list.find(c => c.name.trim().toLocaleLowerCase("vi") === name.toLocaleLowerCase("vi"));
    try {
      let list = await loadDocumentCategories(true);
      let category = find(list);
      if (!category) {
        try {
          category = await internalApi<DocumentCategory>("/document-categories", "POST", {
            name, active: true, effectiveLabel: "Ngày văn bản", effectiveRequired: false,
            expireShown: false, itemsLabel: "Nội dung"
          });
        } catch (e) {
          list = await loadDocumentCategories(true);
          category = find(list);
          if (!category) throw e;
        }
      }
      if (!category.active) throw new Error("Loại văn bản đã bị ẩn. Hãy bật lại trong danh mục Loại văn bản.");
      if (parent && category.id === 1) throw new Error("Mẫu con cần loại văn bản khác Hợp đồng.");
      setUpdatedCategories([...list.filter(c => c.id !== category.id), category]);
      setDocumentType(category.id); setOtherCategory(false); setCategoryName(""); invalidate();
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  const endpoint = draft ? `/contract-drafts/${draft.id}/word` : inline ? `/contract-drafts/from-template/${inline.templateId}/word` : editTemplate ? `/template-library/${editTemplate.id}` : "/contract-templates/word";
  const [mobile, setMobile] = useState(false);
  const [tab, setTab] = useState("edit");
  const [previewCurrent, setPreviewCurrent] = useState(false);
  useEffect(() => {
    const query = window.matchMedia("(max-width: 850px)");
    const update = () => setMobile(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  const [open, setOpen] = useState(!!editTemplate || !!startOpen);
  const [stamp, setStamp] = useState("");
  const [editFields, setEditFields] = useState<TemplateDefinition["fields"]>([]);
  const [manual, setManual] = useState(true);
  function closeEditor() { setOtherCategory(false); setCategoryName(""); setOpen(false); onClose?.(); }
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [source, setSource] = useState<{ fileName: string; base64: string } | null>(null);
  const [extraction, setExtraction] = useState<Extraction | null>(null);
  const [name, setName] = useState("");
  const [documentType, setDocumentType] = useState(1);
  const [cards,setCards] = useState<Card[]>([]);
  const [blocks, setBlocks] = useState<Block[]>([]);
  const [tables, setTables] = useState<AddedTable[]>([]);
  const [additions, setAdditions] = useState<AddedContent[]>([]);
  const imageInput = useRef<HTMLInputElement>(null);
  const [tableRows, setTableRows] = useState(2);
  const [tableColumns, setTableColumns] = useState(3);

  const [preview, setPreview] = useState<{ url: string; token: string } | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [active, setActive] = useState("");
  const inputs = useRef<Record<string, HTMLTextAreaElement | null>>({});
  const previewUrl = preview?.url;
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);
  const revision = useRef(0);
  const lastContent = useRef(inline?.content);
  useEffect(() => {
    if (lastContent.current !== inline?.content) {
      lastContent.current = inline?.content;
      revision.current++;
      setPreviewCurrent(false);
    }
  }, [inline?.content]);
  useEffect(() => () => { revision.current++; }, []);
  const [partyWarnings, setPartyWarnings] = useState<string[]>([]);
  const syncedCustomer = useRef<{ extraction: Extraction; values: Map<string, string> } | null>(null);
  useEffect(() => {
    if (!inline || inline.readOnly || !inline.content.customerId || !extraction) return;
    const warnings: string[] = [];
    const values = wordCustomerValues(extraction.blocks, inline.content.fields, inline.content.partyType, warnings);
    setPartyWarnings([...new Set(warnings)]);
    // A new draft takes Bên A from the chosen customer. A saved draft keeps its Word text as stored: only Bên A values
    // that differ from what was saved (customer details refilled after opening it) are written into the Word content.
    const previous = syncedCustomer.current?.extraction === extraction ? syncedCustomer.current.values
      : draft && inline.saved ? wordCustomerValues(extraction.blocks, inline.saved.fields, inline.saved.partyType) : null;
    const changed = new Map([...values].filter(([id, value]) => previous?.get(id) !== value));
    syncedCustomer.current = { extraction, values };
    if (!changed.size) return;
    setBlocks(current => current.map(block => changed.has(block.id) ? { ...block, text: changed.get(block.id)! } : block));
    invalidate();
  }, [inline?.content, inline?.readOnly, draft, extraction]);

  useEffect(() => {
    if (!inline || !extraction) return;
    const number = inline.content.contractNumber || "Chờ cấp số khi lưu nháp";
    const changes = new Map<string, string>();
    for (const block of extraction.blocks.filter(block => block.bodyUnit != null)) {
      if (/^\s*(căn\s+cứ|điều\s+\d|hôm\s+nay)/iu.test(block.text)) break;
      if (/^\s*Số(?:\s+hợp\s+đồng)?\s*[:：]/iu.test(block.text)) {
        changes.set(block.id, block.text.replace(/^(\s*Số(?:\s+hợp\s+đồng)?\s*[:：]\s*).*$/iu, "$1" + number));
        break;
      }
    }
    setBlocks(current => current.map(block => changes.has(block.id) ? { ...block, text: changes.get(block.id)! } : block));
  }, [extraction, inline?.content.contractNumber]);

  function invalidate(edit = true) { if (inline && edit) { revision.current++; inline.receipt.current = null; inline.changed(); } setPreviewCurrent(false); setConfirmed(false); setError(""); }
  async function extract(file?: File) {
    if (!file) return;
    invalidate(); setPreview(null); setSource(null); setExtraction(null); setMessage(""); setTables([]); setAdditions([]);
    if (!/\.(doc|docx)$/i.test(file.name) || file.size === 0 || file.size > 10 * 1024 * 1024) {
      setError("Chọn Word .doc hoặc .docx có nội dung, tối đa 10 MB."); return;
    }
    setBusy(true);
    try {
      const base64 = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).split(",")[1]);
        reader.onerror = () => reject(new Error("Không đọc được file."));
        reader.readAsDataURL(file);
      });
      const original = { fileName: file.name, base64 };
      const result = await internalApi<Extraction>("/contract-templates/word/extract", "POST", original);
      setSource(original); setExtraction(result); setName(editTemplate ? name : replace?.name ?? result.name); setBlocks(result.blocks);
      setActive("");
      const guessed = /ĐNTT|DNTT/i.test(file.name) ? 5 : /BienBan|Biên bản/i.test(file.name) ? 3 : /PLHD|PLHĐ|PhuLuc|Phụ lục/i.test(file.name) ? 2 : 1;
      // Editing or replacing keeps the template's type; a child template is never a contract.
      const type = editTemplate ? documentType : replace ? replace.documentType : parent && guessed === 1 ? 2 : guessed;
      setDocumentType(type);
      await render({
        ...original, documentBase64: result.documentBase64, sourceToken: result.sourceToken,
        name: editTemplate ? name : replace?.name ?? result.name, documentType: type,
        blocks: result.blocks.map(({ images, ...block }) => { void images; return block; }),
        fields: editFields, tables: [], additions: [], sampleValues: {}, manualContent: manual,
      }, false);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  function payload() {
    const original = new Set(extraction!.blocks.map(b=>b.id));
    const retained = new Set(cards.flatMap(c=>c.ids));
    const byId = new Map(blocks.map(b=>[b.id,b]));
    const bodyOrder = inline ? [...new Set(cards.flatMap(c=>c.ids.map(id=>byId.get(id)!.bodyUnit!)))].map(id=>({id,text:original.has(id)?undefined:byId.get(id)!.text,heading:cards.some(c=>c.title===id&&!c.fixed)})) : undefined;
    return { ...source!, documentBase64: extraction!.documentBase64, sourceToken: extraction!.sourceToken,
      name, documentType, bodyOrder, blocks: blocks.filter(b=>original.has(b.id)).map(({ images, ...block }) => { void images; return block; }), fields: editFields, tables:tables.filter(t=>!inline||retained.has(t.afterBlockId)), additions:additions.filter(a=>!inline||retained.has(a.afterBlockId)), sampleValues: {}, manualContent: manual };
  }
  async function render(input: ReturnType<typeof payload> | Omit<ReturnType<typeof payload>,"bodyOrder"> = payload(), showPreview = true) {
    const version = revision.current; setBusy(true); invalidate(false);
    try {
      const result = await internalApi<{ pdfBase64: string; previewToken: string }>(`${endpoint}/${inline ? "editor-preview" : "preview"}`, "POST", inline ? { document:input, content:{...inline.content,customerId:inline.content.customerId || "0",templateSnapshot:null} } : input);
      if (revision.current !== version) return;
      setPreview({ url: URL.createObjectURL(wordBlob(result.pdfBase64, "application/pdf")), token: result.previewToken });
      setPreviewCurrent(true);
      if (inline && inline.receipt.current !== undefined) inline.receipt.current = result.previewToken;
      if (showPreview) setTab("preview");
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function activate() {
    if (otherCategory || !preview || !previewCurrent || !confirmed) return;
    setBusy(true); setError("");
    try {
      const template = editTemplate
        ? await internalApi<TemplateDefinition>(`/template-library/${editTemplate.id}?stamp=${stamp}`, "PUT", { input: payload(), previewToken: preview.token })
        : await internalApi<TemplateDefinition>("/contract-templates/word/activate", "POST",
          { input: payload(), previewToken: preview.token, parentTemplateId: parent?.id ?? null, replaceTemplateId: replace?.id ?? null });
      onImported(template);
      setMessage((editTemplate ? "Đã lưu thay đổi mẫu " : replace ? "Đã thay file Word của mẫu " : "Đã kích hoạt mẫu ") + name + ".");
      closeEditor(); invalidate();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  useEffect(() => {
    if (!editTemplate) return;
    let active = true;
    async function load(id: string) {
      setBusy(true);
      try {
        const { extraction: result, stamp, template } = await internalApi<{extraction: Extraction & {coreFields:{key:string}[]}; stamp:string; template:TemplateDefinition}>(`/template-library/${id}/editor`);
        const type = template.documentType ?? 1;
        if (!active) return;
        const original = {fileName:"template.docx", base64:result.documentBase64};
        const fields = template.fields.filter(f => !result.coreFields.some(core => core.key === f.key));
        const manualContent = template.manualWordContent ?? false;
        setStamp(stamp); setEditFields(fields); setManual(manualContent); setSource(original); setExtraction(result);
        setBlocks(result.blocks); setName(template.name); setDocumentType(type);
        await render({...original, documentBase64:result.documentBase64, sourceToken:result.sourceToken,
          name:template.name, documentType:type, blocks:result.blocks.map(({images,...b})=>b), fields, tables:[], additions:[],
          sampleValues:{}, manualContent}, false);
      } catch(e) { if (active) setError((e as Error).message); }
      finally { if (active) setBusy(false); }
    }
    void load(editTemplate.id);
    return () => {active=false;};
  }, [editTemplate?.id]);
  async function openEditor() {
    setOpen(true); setMessage("");
    if (!draft && !inline) return;
    setBusy(true); invalidate(false); setPreview(null); setTables([]); setAdditions([]);
    try {
      const result = inline?.readOnly ? await internalApi<Extraction>(`${endpoint}/content`) : await internalApi<Extraction>(`${endpoint}/extract`, "POST", {});
      setCards(wordCards(result.blocks));
      setSource({fileName:"hop-dong.docx",base64:result.documentBase64});
      setExtraction(result); setName(editTemplate ? name : result.name); setBlocks(result.blocks); setDocumentType(1); setActive("");
      if (inline && !inline.readOnly) await render({fileName:"hop-dong.docx", base64:result.documentBase64, documentBase64:result.documentBase64, sourceToken:result.sourceToken, name:result.name, documentType:1, blocks:result.blocks.map(({images,...b}) => b), fields:[], tables:[], additions:[], sampleValues:{}, manualContent:true}, false);
    } catch(e) { setError((e as Error).message); } finally {setBusy(false);}
  }
  useEffect(() => { if (inline) void openEditor(); }, [endpoint]);
  const located = useRef<number | null>(null);
  useEffect(() => { if (!open || !locate?.location || busy || located.current === locate.request) return; const input = inputs.current[locate.location] ?? document.querySelector<HTMLElement>(`[data-word-ids~="${CSS.escape(locate.location)}"]`); if(input){input.scrollIntoView({block:"center"}); input.focus(); located.current=locate.request;} }, [open, busy, blocks, locate]);
  const activeBlock = blocks.find(b => b.id === active);
  function insertContractValue(key: string) {
    if (!activeBlock || !key) return;
    const input = inputs.current[activeBlock.id];
    const start = input?.selectionStart ?? activeBlock.text.length;
    const end = input?.selectionEnd ?? start;
    const text = activeBlock.text.slice(0, start) + "{{" + key + "}}" + activeBlock.text.slice(end);
    setBlocks(current => current.map(block => block.id === activeBlock.id ? { ...block, text } : block));
    invalidate();
    input?.focus();
  }
  const headings = blocks.filter(b => b.heading && b.text.trim());
  function addTable() {
    if (!activeBlock || !activeBlock.location.startsWith("Nội dung")) return;
    // Insert after the whole existing table when an original table cell is selected.
    const anchor = activeBlock.tableId == null ? activeBlock : blocks.filter(b => b.tableId === activeBlock.tableId).at(-1)!;
    setTables(old => [...old, {...createTable(anchor.id, tableRows, tableColumns), order:nextOrder()}]); invalidate();
  }
  function nextOrder() { return Math.max(0,...tables.map(t => t.order ?? 0),...additions.map(a => a.order)) + 1; }
  function anchorId() { return activeBlock?.tableId == null ? activeBlock?.id : blocks.filter(b => b.tableId === activeBlock.tableId).at(-1)?.id; }
  function addContent(kind: Exclude<AddedContent["kind"], "image">) {
    const afterBlockId = anchorId(); if (!afterBlockId) return;
    setAdditions(old => [...old,{id:crypto.randomUUID(),afterBlockId,kind,title:kind === "definition" ? "Định nghĩa" : kind === "appendix" ? "Phụ lục" : "",text:"",widthCm:12,align:"center",order:nextOrder()}]); invalidate();
  }
  async function addImage(file?: File) {
    const afterBlockId = anchorId(); if (!file || !afterBlockId) return;
    if (!["image/png","image/jpeg"].includes(file.type) || file.size > 5*1024*1024 || !file.size) {setError("Chọn ảnh PNG/JPEG, tối đa 5 MB.");return;}
    if (additions.reduce((size,item) => size + (item.imageBase64?.length ?? 0)*0.75, 0) + file.size > 10*1024*1024) {setError("Tổng ảnh chèn thêm tối đa 10 MB.");return;}
    setBusy(true); setError("");
    try {
      const base64 = await new Promise<string>((resolve,reject) => {const reader=new FileReader(); reader.onload=()=>resolve(String(reader.result).split(",")[1]); reader.onerror=()=>reject(new Error("Không đọc được ảnh.")); reader.readAsDataURL(file);});
      setAdditions(old => [...old,{id:crypto.randomUUID(),afterBlockId,kind:"image",title:"",text:"",imageBase64:base64,imageMime:file.type,widthCm:12,align:"center",order:nextOrder()}]); invalidate();
    } catch(e) {setError((e as Error).message);} finally {setBusy(false); if(imageInput.current) imageInput.current.value="";}
  }
  const canInsert = !busy && !!activeBlock?.location.startsWith("Nội dung") && additions.length < 50;
  function insertedAfter(block: Block) {
    return [...tables.filter(t => t.afterBlockId === block.id).map(t => ({order:t.order ?? 0,id:t.id,table:t,content:null})),
      ...additions.filter(a => a.afterBlockId === block.id).map(a => ({order:a.order,id:a.id,table:null,content:a}))]
      .sort((a,b) => a.order-b.order).map(item => item.table ? <WordTableEditor key={item.id} table={item.table} disabled={busy}
        onChange={value => {setTables(old => old.map(t => t.id === value.id ? value : t)); invalidate();}}
        onDelete={() => {setTables(old => old.filter(t => t.id !== item.id)); invalidate();}}/> : <WordContentEditor key={item.id} value={item.content!} disabled={busy}
        onChange={value => {setAdditions(old => old.map(a => a.id === value.id ? value : a)); invalidate();}}
        onDelete={() => {setAdditions(old => old.filter(a => a.id !== item.id)); invalidate();}}/>);
  }
  const editor = <section className={styles.pane} aria-label="Khung chỉnh mẫu">
    <div className={styles.toolbar}>
      <strong>Chỉnh nội dung</strong>
      <select aria-label="Đi tới mục trong tài liệu" className={styles.select} value="" onChange={e => {
        const target = inputs.current[e.target.value]; target?.focus(); target?.scrollIntoView({ block: "center", behavior: "smooth" });
      }}><option value="">Đi tới mục…</option>{headings.map(b => <option value={b.id} key={b.id}>{b.text.slice(0, 100)}</option>)}</select>
      {extraction && <>
        <span className={styles.selection}>Bấm vào nội dung để sửa trực tiếp. Chọn một đoạn để chèn nội dung bên dưới.</span>
        {documentType === 1 && <label>Điền từ hồ sơ <select disabled={busy || !activeBlock} value="" onChange={e => insertContractValue(e.target.value)}>
          <option value="">Chọn chữ trong Word rồi chọn giá trị</option>
          <option value="CONTRACT_NUMBER">Số hợp đồng</option>
          <option value="VAT">Tiền VAT</option>
          <option value="TOTAL_AMOUNT">Tổng sau VAT</option>
          <option value="PAYMENT_METHOD">Phương thức thanh toán</option>
          <option value="PAYMENT_SCHEDULE">Lịch thanh toán</option>
        </select></label>}
        <div className="flex w-full flex-wrap gap-2">
          <select aria-label="Thêm mục sau vị trí chọn" className={styles.select} disabled={!canInsert} value="" onChange={e => {
            const kind = e.target.value;
            if (kind === "paragraph" || kind === "clause" || kind === "definition" || kind === "appendix") addContent(kind);
          }}>
            <option value="">Thêm mục sau vị trí chọn</option>
            <option value="paragraph">Đoạn văn</option>
            <option value="clause">Điều khoản</option>
            <option value="definition">Định nghĩa</option>
            <option value="appendix">Phụ lục</option>
          </select>
          <Button type="button" variant="outline" disabled={!canInsert} onClick={() => imageInput.current?.click()}>Chèn ảnh</Button>
          <input ref={imageInput} type="file" aria-label="Ảnh chèn thêm" accept="image/png,image/jpeg" hidden disabled={!canInsert} onChange={e => void addImage(e.target.files?.[0])}/>
        </div>
        <div className="flex w-full flex-wrap items-center gap-2">
          <label>Hàng <input aria-label="Số hàng bảng mới" className="w-14 rounded border px-1" type="number" min={1} max={50} value={tableRows} disabled={busy} onChange={e => setTableRows(Math.max(1, Math.min(50, Number(e.target.value) || 1)))}/></label>
          <label>Cột <input aria-label="Số cột bảng mới" className="w-14 rounded border px-1" type="number" min={1} max={12} value={tableColumns} disabled={busy} onChange={e => setTableColumns(Math.max(1, Math.min(12, Number(e.target.value) || 1)))}/></label>
          <Button type="button" variant="outline" disabled={busy || tables.length >= 20 || !activeBlock?.location.startsWith("Nội dung")} onClick={addTable}>Chèn bảng sau vị trí chọn</Button>
        </div>
      </>}
    </div>
    <div className={styles.scroll}>
      {extraction ? <div className={styles.paper}><WordDocumentLayout blocks={blocks}
        afterBlock={insertedAfter} renderBlock={block => <>
        {block.images?.map((image,index) => <div key={index}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={`data:${image.mime};base64,${image.base64}`} alt="Ảnh có sẵn trong mẫu Word" style={{maxWidth:"100%",height:"auto"}}/>
        </div>)}
        <textarea aria-label={block.text.trim().slice(0,60) || "Nội dung trống"} data-block-id={block.id}
          className={/^(Điều\s+\d|HỢP ĐỒNG|BIÊN BẢN|CỘNG HOÀ|CỘNG HÒA)/i.test(block.text.trim()) ? styles.heading : ""}
          ref={el => {inputs.current[block.id] = el;}} value={block.text} rows={1} maxLength={20000} disabled={busy}
          onFocus={() => setActive(block.id)}
          onChange={e => {setBlocks(old => old.map(b => b.id === block.id ? {...b,text:e.target.value} : b)); invalidate();}}/>
      </>}/></div> : <p className={styles.empty}>Chọn file Word để mở tài liệu.</p>}
    </div>
  </section>;
  const previewPane = <section className={styles.pane} aria-label="Khung xem trước">
    <div className={styles.toolbar}><strong>Bản in từ Word</strong>
      {extraction && <Button type="button" variant="outline" disabled={busy} onClick={() => void render()}>Cập nhật bản xem trước</Button>}
      <p className={styles.selection} role="status">{busy ? "Đang xử lý tài liệu…" : previewCurrent ? "Bản in đã cập nhật." : extraction ? "Cập nhật bản xem trước để kiểm tra các thay đổi." : "Bố cục, ảnh và đường viền được lấy từ file Word."}</p>
    </div>
    <div className={styles.scroll}>
      {preview ? <><a href={preview.url} target="_blank" rel="noreferrer">Mở PDF</a><WordPdfViewer key={preview.url} url={preview.url}/></> : <p className={styles.empty}>{busy ? "Đang chuyển Word sang bản xem trước…" : extraction ? "Chưa có bản in. Bấm Cập nhật bản xem trước để thử lại." : "Chọn file Word để xem bản in tại đây."}</p>}
    </div>
  </section>;
  if (inline) return <>
    {partyWarnings.map(message => <p key={message} role="status">{message}</p>)}
    {error && <p role="alert">{error}</p>}
    {!extraction && !busy && <Button type="button" onClick={() => void openEditor()}>Tải lại nội dung Word</Button>}
    {busy && !extraction && <p role="status">Đang đọc nội dung Word…</p>}
    {extraction && <WordClauseForm cards={cards} blocks={blocks} select={setActive} active={active} readOnly={inline.readOnly} busy={busy}
      update={(next,values)=>{setCards(next);setBlocks(values);invalidate();}} anchors={new Set([...tables,...additions].map(a=>a.afterBlockId))}
      after={insertedAfter} table={addTable} image={()=>imageInput.current?.click()}/>}
    <input type="file" aria-label="Ảnh chèn thêm" ref={imageInput} hidden accept="image/png,image/jpeg" onChange={e=>void addImage(e.target.files?.[0])}/>
    {inline.previewHost && createPortal(previewPane, inline.previewHost)}
  </>;
  return <>
    {!editTemplate && (trigger ? trigger(() => void openEditor()) : <Button type="button" variant="outline" disabled={disabled} onClick={() => void openEditor()}>Tạo mẫu văn bản</Button>)}
    {message && !trigger && !editTemplate && <p role="status">{message}</p>}
    <Dialog open={open} onOpenChange={value => {if (busy) return; if (value) setOpen(true); else closeEditor();}}>
      <DialogContent className={styles.dialog} style={{width:"98vw",maxWidth:"98vw",height:"96dvh",display:"flex",overflow:"hidden"}} onInteractOutside={e => e.preventDefault()}>
        <DialogHeader><DialogTitle>{editTemplate ? `Sửa nội dung mẫu · ${editTemplate.name}` : replace ? `Thay file Word · ${replace.name}` : parent ? `Tạo mẫu con cho ${parent.name}` : "Tạo mẫu văn bản"}</DialogTitle><DialogDescription>Bên trái sửa nội dung trực tiếp, thêm điều khoản, bảng và ảnh. Bên phải xem bản in giữ bố cục của file Word.</DialogDescription></DialogHeader>
        <fieldset disabled={busy} className={styles.metadata} hidden={!!draft}>
          <label><span>File Word (.doc, .docx, tối đa 10 MB)<span className={styles.required} aria-hidden>*</span></span><Input type="file" accept=".doc,.docx" onChange={e => void extract(e.target.files?.[0])}/></label>
          <label><span>Tên mẫu<span className={styles.required} aria-hidden>*</span></span><Input value={name} maxLength={500} placeholder="Ví dụ: Hợp đồng cung cấp phần mềm 2026" onChange={e => {setName(e.target.value); invalidate();}}/></label>
          <label><span>Loại mẫu<span className={styles.required} aria-hidden>*</span></span><select className={styles.select} value={otherCategory ? "other" : documentType} onChange={e => {if (e.target.value === "other") setOtherCategory(true); else { setOtherCategory(false); setDocumentType(Number(e.target.value)); } invalidate();}}>
            {typeOptions(updatedCategories ?? categories, documentType).map(c => <option value={c.id} key={c.id} disabled={!!parent && c.id === 1}>{c.name}</option>)}
            <option value="other">Khác…</option>
          </select></label>
          {otherCategory && <div>
            <label>Tên loại văn bản<Input value={categoryName} maxLength={200} onChange={e => setCategoryName(e.target.value)}/></label>
            <Button type="button" disabled={!categoryName.trim()} onClick={() => void chooseCategory()}>Dùng loại này</Button>
          </div>}
        </fieldset>
        <div className={styles.tabs}><Button type="button" variant={tab==="edit"?"default":"outline"} onClick={() => setTab("edit")}>Chỉnh mẫu</Button><Button type="button" variant={tab==="preview"?"default":"outline"} onClick={() => setTab("preview")}>Xem trước</Button></div>
        <div className={styles.workspace}>{mobile ? <><div className={styles.mobilePane} hidden={tab!=="edit"}>{editor}</div><div className={styles.mobilePane} hidden={tab!=="preview"}>{previewPane}</div></> :
          <PanelGroup direction="horizontal"><Panel defaultSize={50} minSize={30}>{editor}</Panel><PanelResizeHandle className={styles.handle} aria-label="Điều chỉnh chiều rộng hai khung">⋮</PanelResizeHandle><Panel defaultSize={50} minSize={30}>{previewPane}</Panel></PanelGroup>}
        </div>
        {busy && <p role="status">Đang xử lý tài liệu…</p>}
        {error && <p role="alert" className="text-red-600">{error}</p>}
        <div className={styles.footer}>
          {preview && <label><input type="checkbox" checked={confirmed} disabled={busy || !previewCurrent} onChange={e => setConfirmed(e.target.checked)}/> Tôi đã kiểm tra nội dung và bố cục PDF đối chiếu.</label>}
          <Button type="button" variant="outline" disabled={busy} onClick={closeEditor}>Đóng</Button>
          <Button type="button" disabled={busy || otherCategory || !preview || !previewCurrent || !confirmed} onClick={() => void activate()}>{editTemplate ? "Lưu thay đổi" : "Kích hoạt mẫu"}</Button>
        </div>
      </DialogContent>
    </Dialog>
  </>;
}
