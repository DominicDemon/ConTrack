import ReviewView from "@/components/portal/review-view";
export default async function CustomerReviewPage({ params }: { params: Promise<{ reviewId: string }> }) { const { reviewId } = await params; return <ReviewView id={reviewId}/>; }
