import ReviewLink from "@/components/portal/review-link";
export default async function CustomerReviewLinkPage({ params }: { params: Promise<{ token: string }> }) { const { token } = await params; return <ReviewLink token={token}/>; }
