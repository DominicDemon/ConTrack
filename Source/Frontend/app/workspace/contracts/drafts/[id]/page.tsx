import DraftEditor from "@/components/internal/draft-editor";
export default async function EditDraftPage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <DraftEditor id={id}/>; }
