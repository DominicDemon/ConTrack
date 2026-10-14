import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Descriptions, Spin } from 'antd';
import { getHealth } from '../../api/health';

/** Trang kiểm tra môi trường: React gọi được API và API kết nối được Master DB. */
export function DashboardPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['health'], queryFn: getHealth });

  return (
    <Card title="Trạng thái môi trường">
      {isLoading && <Spin />}
      {isError && <Alert type="error" message="Không gọi được API. Kiểm tra API đã chạy ở http://localhost:5080." />}
      {data && (
        <Descriptions column={1} bordered size="small">
          <Descriptions.Item label="API">{data.status}</Descriptions.Item>
          <Descriptions.Item label="Master DB">{data.masterDb}</Descriptions.Item>
          <Descriptions.Item label="Thời gian máy chủ">{new Date(data.time).toLocaleString('vi-VN')}</Descriptions.Item>
        </Descriptions>
      )}
    </Card>
  );
}
