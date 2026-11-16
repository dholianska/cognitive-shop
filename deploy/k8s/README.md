# Лаб. 10: Розгортання в Kubernetes

Маніфести для ручного розгортання `catalog-api` і `gateway` у локальному кластері Docker Desktop.

## Образи

```bash
docker build -t catalog-api:v1 -f Services/Catalog/Catalog.Api/Dockerfile .
docker build -t gateway:v1 -f Platform.ApiGateway/Dockerfile .
```

## Секрети (не зберігаються в репозиторії)

```bash
kubectl create secret generic catalog-secrets \
  --from-literal=ConnectionStrings__CatalogDb="$CATALOG_DB" \
  --from-literal=Jwt__SecretKey='<ключ підпису JWT>' \
  --from-literal=ConnectionStrings__redis='<рядок підключення Redis>' \
  --from-literal=ConnectionStrings__messaging='<рядок підключення RabbitMQ>'

kubectl create secret generic gateway-secrets \
  --from-literal=Jwt__SecretKey='<ключ підпису JWT>'
```

## Застосування

```bash
kubectl apply -f configmap.yaml
kubectl apply -f catalog-api-deployment.yaml -f catalog-api-service.yaml
kubectl apply -f gateway-deployment.yaml -f gateway-service.yaml
kubectl get pods,services
```

## Перевірка

```bash
kubectl port-forward service/gateway 8080:80
# http://localhost:8080/api/products (потрібен JWT)
```